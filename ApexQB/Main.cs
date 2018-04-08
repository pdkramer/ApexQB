using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using System.IO;
using System.Xml.Serialization;
using System.Data.SqlClient;
using System.Configuration;
using Interop.QBXMLRP2;
using System.Text.RegularExpressions;

namespace ApexQB
{
    public partial class frmMain : DevExpress.XtraEditors.XtraForm
    {
        private const string BUILDDATE = "4/5/2018";
        private string _Response;
        private RequestProcessor2 _Rp;  //QuickBooks request processor
        private string _Ticket;
        private SqlConnectionStringBuilder _SqlConnBuilder;
        private static List<StatusLine> _StatusLines = new List<StatusLine>();  //Status report lines
        private string _QBCompanyName = String.Empty;
        private string _ApexTargetCompany = String.Empty;
        private bool _InvoicesSent = false;
        private Regex _JobRegEx = new Regex(@"([EMT]\d{4}[A-Z]{3}\d[A-Z]\d{1,2})\s*(.*)");  //Format of job identifiers used by the client

        //These two fields are specific to client rules and Apex, not the QuickBooks interface
        private double _MaxDiffPct;  //Maximum percent an invoice can deviate from exactly reconciled and still be sent to QuickBooks
        private decimal _MaxDiffAmt; //Maximum amount an invoice can deviate from exactly reconciled and still be sent to QuickBooks

#if DEBUG
        //In DEBUGMODE we write the XML files used in the conversation to a specific disk directory that is assumed to exist
        //This tremendously helps during troubleshooting
        private const bool DEBUGMODE = true;
#else
      private const bool DEBUGMODE = false;
#endif

        public frmMain()
        {
            InitializeComponent();
        }

        public IEnumerable<StatusLine> GetStatusLines()
        {
            return _StatusLines;
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            StatusLabel.Text = "Rev: " + BUILDDATE;
            if (DEBUGMODE) StatusLabel.Text += " (Debug)";

            ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings["ApexQB.Properties.Settings.ApexConnectionString"];
            if (settings != null)
            {
                string connection = settings.ConnectionString;
                _SqlConnBuilder = new SqlConnectionStringBuilder(connection);

                _SqlConnBuilder.DataSource = Properties.Settings.Default.ApexServer;
                _SqlConnBuilder.InitialCatalog = Properties.Settings.Default.ApexDatabase;

                //See if we have all of the requisite tables built and Apex is at the minimum version
                using (System.Data.SqlClient.SqlConnection conn = new SqlConnection(_SqlConnBuilder.ConnectionString))
                {
                    conn.Open();
                    System.Data.SqlClient.SqlCommand cmd;
                    cmd = new System.Data.SqlClient.SqlCommand("SELECT Version FROM System", conn);
                    int version = (int?)cmd.ExecuteScalar() ?? 0;
                    if (version < 36)
                    {
                        MessageBox.Show("This program requires Apex with database version 36 or greater.");
                        Application.Exit();
                    }

                    //Find the maximum allowable difference percentage or set a default
                    cmd = new System.Data.SqlClient.SqlCommand("SELECT PropVal FROM PropBag WHERE PropName = 'QBMaxDiffPct'", conn);
                    string QBMaxDiffPct = (string)cmd.ExecuteScalar();
                    if (QBMaxDiffPct != null)
                        _MaxDiffPct = Double.Parse(QBMaxDiffPct);
                    else
                        _MaxDiffPct = 5;

                    //Find the maximum allowable difference amount or set a default
                    cmd = new System.Data.SqlClient.SqlCommand("SELECT PropVal FROM PropBag WHERE PropName = 'QBMaxDiffAmt'", conn);
                    string QBMaxDiffAmt = (string)cmd.ExecuteScalar();
                    if (QBMaxDiffAmt != null)
                        _MaxDiffAmt = Decimal.Parse(QBMaxDiffAmt);
                    else
                        _MaxDiffAmt = 25;

                    //Custom QBVendor and QBJob tables are used by this program in the Apex database -- Build them if necessary
                    cmd = new SqlCommand("SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'QBVendor'", conn);
                    int tablecount = (int?)cmd.ExecuteScalar() ?? 0;
                    if (tablecount < 1) BuildExpTables(conn);

                    //Custom QBInvoice used by this program in the Apex database -- Build them if necessary
                    cmd = new SqlCommand("SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'QBInvoice'", conn);
                    tablecount = (int?)cmd.ExecuteScalar() ?? 0;
                    if (tablecount < 1) BuildQBInvoiceTable(conn);

                    conn.Close();
                }
            }
            else
            {
                MessageBox.Show("Unable to set connection string, program terminating.");
                Application.Exit();
                return;
            }
        }

        private void BuildQBInvoiceTable(SqlConnection conn)
        {
            const string buildQBInvoiceSQL = @"CREATE TABLE [dbo].[QBInvoice](
                [Invoice] [varchar](15) NOT NULL,
                [PO] [varchar](12) NOT NULL,
                [SentDate] [datetime] NULL,
                CONSTRAINT [PK_QBInvoice] PRIMARY KEY CLUSTERED
                (
                    [Invoice] ASC,
                    [PO] ASC
                )  ON [PRIMARY]
                )  ON [PRIMARY]";

            System.Data.SqlClient.SqlCommand cmd;
            cmd = new System.Data.SqlClient.SqlCommand(buildQBInvoiceSQL, conn);
            cmd.ExecuteNonQuery();

            //Load all "Paid" invoices so that we can eliminate duplicates from now on
            cmd = new SqlCommand("INSERT INTO QBInvoice (Invoice, PO) SELECT Invoice, PO FROM VendIvc WHERE IvcStatus = 'P' ", conn);
            cmd.ExecuteNonQuery();
        }

        private void BuildExpTables(SqlConnection conn)
        {
            const string buildVendorSQL = @"CREATE TABLE [dbo].[QBVendor](
	            [ApexCompany] [varchar](3) NOT NULL,
	            [ApexVendorID] [varchar](6) NOT NULL,
                [QBListID] [varchar] (50) NOT NULL,
	            [QBVendorName] [varchar](41) NOT NULL,
	            [Terms] [varchar](31) NULL,
                CONSTRAINT [PK_QBVendor] PRIMARY KEY CLUSTERED 
                (
   	                [ApexCompany] ASC,
	                [ApexVendorID] ASC
                ) ON [PRIMARY]
                ) ON [PRIMARY]";

            const string buildJobSQL = @"CREATE TABLE [dbo].[QBJob](
	            [ApexCompany] [varchar](3) NOT NULL,
	            [ApexJobID] [varchar](12) NOT NULL,
                [QBListID] [varchar] (50) NOT NULL,
                [QBJobName] [varchar](209) NOT NULL,
                CONSTRAINT [PK_] PRIMARY KEY CLUSTERED 
                    (
                        [ApexCompany] ASC,
	                    [ApexJobID] ASC
                    ) ON [PRIMARY]
                    ) ON [PRIMARY]";

            System.Data.SqlClient.SqlCommand cmd;
            cmd = new System.Data.SqlClient.SqlCommand(buildVendorSQL, conn);
            cmd.ExecuteNonQuery();
            cmd = new System.Data.SqlClient.SqlCommand(buildJobSQL, conn);
            cmd.ExecuteNonQuery();
        }

        private string GetApexTargetCompany(string QBCompanyName)
        {
            string co = String.Empty;

            //Using the QuickBooks file name, map it to the correct company identifier for Apex
            switch (QBCompanyName.ToLower())
            {
                case "cet.qbw":
                case "cet(test).qbw":
                    co = "T";
                    break;
                case "cem.qbw":
                case "cem(test).qbw":
                    co = "M";
                    break;
                case "cedb.qbw":
                case "cedb(test).qbw":
                    co = "E";
                    break;
                default:
                    MessageBox.Show("Invalid company open in QuickBooks.  Processing cannot continue.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    co = "*";
                    break;
            }
            return co;
        }

        private void btnSendIvcs_Click(object sender, EventArgs e)
        {
            UpdateStatus("Invoice export in progress...");
            Cursor.Current = Cursors.WaitCursor;
            _InvoicesSent = false;

            try
            {
                SendApexInvoices();
            }
            finally
            {
                Cursor.Current = Cursors.Default;
            }

            if (_InvoicesSent)
            {
                var statusReportViewer = new frmStatusReport();
                statusReportViewer.ShowDialog();
            }
        }

        protected void SendApexInvoices()
        {
            using (ApexDataDataContext apexData = new ApexDataDataContext(_SqlConnBuilder.ConnectionString))
            {
                try
                {
                    _Rp = new RequestProcessor2();
                    _Rp.OpenConnection("", "Apex Interface");
                    _Ticket = _Rp.BeginSession("", Interop.QBXMLRP2.QBFileMode.qbFileOpenDoNotCare);
                    _QBCompanyName = Path.GetFileName(_Rp.GetCurrentCompanyFileName(_Ticket));
                    _ApexTargetCompany = GetApexTargetCompany(_QBCompanyName);
                    if (_ApexTargetCompany == "*") //invalid company open
                    {
                        Application.Exit();
                        return;
                    }
                    lblApexCompany.Text = "Quickbooks Company: " + Path.GetFileName(_QBCompanyName);
                    lblApexCompany.Visible = true;
                    lblApexCompany.Refresh();

                    _StatusLines.Clear();

                    List<VendIvc> apexInvoiceList = (from ivc in apexData.VendIvcs
                                                     join po in apexData.POs on ivc.PO equals po.Po1
                                                     where ivc.IvcStatus == "A"
                                                       && po.Company == _ApexTargetCompany
                                                     select ivc).ToList();

                    if (apexInvoiceList.Count == 0)
                    {
                        MessageBox.Show("There are no invoices to send.");
                    }
                    else
                    {
                        _InvoicesSent = true;  //We have a valid invoice to send so present the interface status report when complete

                        GLAcctUtility.GLAcctList = GLAcctUtility.BuildGLAcctList();

                        foreach (VendIvc invoice in apexInvoiceList)
                        {
                            decimal ponet = apexData.POs.Where(s => s.Po1 == invoice.PO).Select(s => s.PoNet).SingleOrDefault() ?? 0;
                            decimal totalInvoiced = apexData.VendIvcs.Where(s => s.PO == invoice.PO).Sum(s => s.IvcAmt) ?? 0;

                            if (apexData.QBInvoices.Where(s => s.Invoice == invoice.Invoice && s.PO == invoice.PO).Any())
                            {
                                _StatusLines.Add(new StatusLine
                                {
                                    Invoice = invoice.Invoice,
                                    PO = invoice.PO.Trim(),
                                    Message = "Duplicate invoice"
                                });
                            }
                            else
                            {
                                if (totalInvoiced != 0 && (totalInvoiced - ponet > _MaxDiffAmt))
                                {
                                    _StatusLines.Add(new StatusLine
                                    {
                                        Invoice = invoice.Invoice,
                                        PO = invoice.PO.Trim(),
                                        Message = $"The total invoiced {totalInvoiced:c} exceeds the P/O Net amount {ponet:c} by over {_MaxDiffAmt:c}"
                                    });
                                }
                                else
                                {
                                    decimal ivcTotal = 0; decimal poTaxableAmt = 0;
                                    foreach (VendIvcL ivcLine in invoice.VendIvcLs)
                                    {
                                        ivcTotal += ivcLine.AmtIvc ?? 0;
                                        POLine poline = apexData.POLines.Where(s => s.Po == ivcLine.PO && s.PoLine1 == ivcLine.POLine).SingleOrDefault();
                                        if (poline?.Taxable == "Y") poTaxableAmt += ivcLine.AmtIvc ?? 0;
                                    }
                                    double poTaxRate = apexData.POs.Where(s => s.Po1 == invoice.PO).Select(s => s.TaxRate).SingleOrDefault() ?? 0;
                                    ivcTotal += ivcTotal * ((decimal)(poTaxRate * 0.01));

                                    decimal invoiceDiff = Math.Abs((invoice.IvcAmt ?? 0) - ivcTotal);

                                    if (ivcTotal != 0 && ((double)((invoiceDiff / ivcTotal)) > (_MaxDiffPct * 0.01)
                                       || invoiceDiff > _MaxDiffAmt))
                                    {
                                        //This enforces a business rule set by the client regarding tolerances when reconciling a vendor invoice
                                        _StatusLines.Add(new StatusLine
                                        {
                                            Invoice = invoice.Invoice,
                                            PO = invoice.PO.Trim(),
                                            Message = $"The P/O lines invoiced {ivcTotal:c} are more than {_MaxDiffAmt:c} or {_MaxDiffPct}% different from the invoice amount {invoice.IvcAmt:c}"
                                        });
                                    }
                                    else
                                    {
                                        //Initial audits passed; process invoice 
                                        ProcessInvoice(invoice, apexData);
                                    }
                                }
                            }
                        }
                    }
                }

                catch (System.Runtime.InteropServices.COMException ex)
                {
                    MessageBox.Show("COM Error Description = " + ex.Message, "COM error");
                    return;
                }

                finally
                {
                    if (_Ticket != null)
                    {
                        _Rp.EndSession(_Ticket);
                    }
                    if (_Rp != null)
                    {
                        _Rp.CloseConnection();
                    }
                    UpdateStatus("Export complete");
                }
            };
        }

        private void ProcessInvoice(VendIvc invoice, ApexDataDataContext apexData)
        {
            PO po = apexData.POs.Where(s => s.Po1 == invoice.PO).SingleOrDefault(); //get the corresponding P/O
            if (po == null)
            {
                _StatusLines.Add(new StatusLine
                {
                    Invoice = invoice.Invoice,
                    PO = invoice.PO.Trim(),
                    Message = "The invoice points to an invalid P/O!?"
                });
                return;
            }
            if (po.Vendor == null)
            {
                _StatusLines.Add(new StatusLine
                {
                    Invoice = invoice.Invoice,
                    PO = invoice.PO.Trim(),
                    Message = "There is no vendor on this P/O"
                });
                return;
            }

            Job job = apexData.Jobs.Where(s => s.Job1 == po.Job).SingleOrDefault(); //get the job
            if (job == null)
            {
                _StatusLines.Add(new StatusLine
                {
                    Invoice = invoice.Invoice,
                    PO = invoice.PO.Trim(),
                    Message = "There is no job on this P/O"
                });
                return;
            }

            QBJob qbjob = apexData.QBJobs.Where(s => s.ApexCompany == _ApexTargetCompany
                                && s.ApexJobID == po.Job).SingleOrDefault();
            if (qbjob == null)
            {
                _StatusLines.Add(new StatusLine
                {
                    Invoice = invoice.Invoice,
                    PO = invoice.PO.Trim(),
                    Message = "This P/O has an invalid QuickBooks job reference"
                });
                return;
            }

            QBVendor qbvendor = apexData.QBVendors.Where(s => s.ApexCompany == _ApexTargetCompany
                                && s.ApexVendorID == po.Vendor).SingleOrDefault();
            if (qbvendor == null)
            {
                _StatusLines.Add(new StatusLine
                {
                    Invoice = invoice.Invoice,
                    PO = invoice.PO.Trim(),
                    Message = "This P/O has an invalid QuickBooks vendor reference"
                });
                return;
            }

            var qbxml = new QBXML();
            qbxml.ItemsElementName = new ItemsChoiceType99[1] { ItemsChoiceType99.QBXMLMsgsRq };
            var qbMsgsRq = new QBXMLMsgsRq();
            qbMsgsRq.onError = QBXMLMsgsRqOnError.continueOnError;

            var billaddrq = new BillAddRqType();
            billaddrq.requestID = "1";

            TermsRef termsref = new TermsRef
            {
                FullName = po.VendorTerms
            };

            string ApexGLRef = apexData.Costcodes
               .Where(s => s.Schedule == "STD" && s.CostCode1 == po.POLines.Select(l => l.CostCode).FirstOrDefault())
               .Select(s => s.GL).FirstOrDefault();

            if (String.IsNullOrEmpty(ApexGLRef)) ApexGLRef = "M";

            string QBGLAcctFullName = GLAcctUtility.GLAcctList
               .Where(s => s.ApexCompany == _ApexTargetCompany && s.ApexGLRef == ApexGLRef)
               .Select(s => s.QBGLAcctFullName).SingleOrDefault();

            AccountRef accountref = new AccountRef
            {
                FullName = QBGLAcctFullName
            };

            AccountRef creditaccountref = new AccountRef
            {
                FullName = "Cash Discount on Payables"
            };

            //Classes in QuickBooks equate to Divisions in Apex for this client
            ClassRef classref = new ClassRef
            {
                FullName = apexData.Divisions.Where(s => s.Company == po.Company && s.Division1 == po.Division).Select(s => s.Name).SingleOrDefault()
            };

            CustomerRef customerref = new CustomerRef
            {
                ListID = qbjob.QBListID
            };

            ExpenseLineAdd expenseline = new ExpenseLineAdd
            {
                AccountRef = accountref,
                Amount = invoice.IvcAmt?.ToString("F2"),
                CustomerRef = customerref,
                Memo = job.Job1 + " " + qbjob.QBJobName.Substring(0, qbjob.QBJobName.IndexOf(':'))
            };

            if (classref.FullName != null) expenseline.ClassRef = classref;

            ExpenseLineAdd[] expenseLines;

            if ((invoice.Discount ?? 0) != 0)  //Add an expense line for the discount amount if the discount is not zero
            {
                ExpenseLineAdd creditexpenseline = new ExpenseLineAdd
                {
                    AccountRef = creditaccountref,
                    Amount = (0 - invoice.Discount ?? 0).ToString("F2"),
                    ClassRef = classref,
                    Memo = job.Job1 + " " + qbjob.QBJobName.Substring(0, qbjob.QBJobName.IndexOf(':'))
                };

                expenseLines = new ExpenseLineAdd[2];
                expenseLines[0] = expenseline;
                expenseLines[1] = creditexpenseline;
            }
            else
            {
                expenseLines = new ExpenseLineAdd[1];
                expenseLines[0] = expenseline;
            }

            VendorRef vendorref = new VendorRef
            {
                ListID = qbvendor.QBListID
            };

            var billadd = new BillAdd
            {
                DueDate = invoice.PayDate?.ToString("yyyy-MM-dd"),
                Memo = "From Apex",
                RefNumber = invoice.Invoice,
                TermsRef = termsref,
                TxnDate = invoice.IvcDate?.ToString("yyyy-MM-dd"),
                ExpenseLineAdd = expenseLines,
                VendorRef = vendorref
            };

            qbMsgsRq.Items = new object[1] { billaddrq };
            qbxml.Items = new object[1] { qbMsgsRq };
            billaddrq.BillAdd = billadd;

            XmlSerializer serializer = new XmlSerializer(typeof(QBXML));
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", ""); //Don't use a namespace in the XML for QuickBooks
            MemoryStream ms = new MemoryStream();

            serializer.Serialize(ms, qbxml, ns);
            ms.Seek(0, SeekOrigin.Begin);
            var sr = new StreamReader(ms);
            string xmlRequest = sr.ReadToEnd();
            xmlRequest = xmlRequest.Replace("<?xml version=\"1.0\"?>", "<?xml version=\"1.0\"?><?qbxml version=\"4.0\"?>");
            if (DEBUGMODE) File.WriteAllText("c:\\QB\\BillAddQBXML.xml", xmlRequest);
            _Response = _Rp.ProcessRequest(_Ticket, xmlRequest);
            if (DEBUGMODE) File.WriteAllText("c:\\QB\\BillAddResponse.xml", _Response);

            QBXML rsXML = GetQbxml(serializer);
            string message = ((BillAddRsType)((QBXMLMsgsRs)rsXML?.Items?[0])?.Items?[0]).statusMessage;
            string statuscode = ((BillAddRsType)((QBXMLMsgsRs)rsXML?.Items?[0])?.Items?[0]).statusCode;

            _StatusLines.Add(new StatusLine
            {
                Invoice = invoice.Invoice,
                PO = invoice.PO.Trim(),
                Message = message,
                StatusCode = statuscode
            });

            if (statuscode == "0") //Apex's part is done now that the invoice has been successfully sent to QuickBooks to be paid
            {
                QBInvoice qbIvc = new QBInvoice
                {
                    Invoice = invoice.Invoice,
                    PO = invoice.PO,
                    SentDate = DateTime.Now
                };
                apexData.QBInvoices.InsertOnSubmit(qbIvc);

                invoice.IvcStatus = "P";
                apexData.SubmitChanges();
            }
        }

        private void UpdateStatus(string newStatus)
        {
            StatusLabel.Text = newStatus;
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private QBXML GetQbxml(XmlSerializer serializer)
        {
            QBXML qbxml;
            using (var reader = new StringReader(_Response))
            {
                qbxml = (QBXML)serializer.Deserialize(reader);
            }
            return qbxml;
        }

        private void btnImport_Click(object sender, EventArgs e)
        {
            try
            {
                _Rp = new RequestProcessor2();
                _Rp.OpenConnection("", "Apex Interface");
                _Ticket = _Rp.BeginSession("", Interop.QBXMLRP2.QBFileMode.qbFileOpenDoNotCare);
                _QBCompanyName = Path.GetFileName(_Rp.GetCurrentCompanyFileName(_Ticket));
                _ApexTargetCompany = GetApexTargetCompany(_QBCompanyName);
                if (_ApexTargetCompany == "*") //invalid company open
                {
                    Application.Exit();
                    return;
                }
                lblApexCompany.Text = "Quickbooks Company: " + Path.GetFileName(_QBCompanyName);
                lblApexCompany.Visible = true;
                lblApexCompany.Refresh();

                //For efficiency we are creating these items once and reusing them for each QuickBooks transfer
                XmlSerializer serializer = new XmlSerializer(typeof(QBXML));
                XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
                ns.Add("", "");


                var qbxml = new QBXML();
                qbxml.ItemsElementName = new ItemsChoiceType99[1] { ItemsChoiceType99.QBXMLMsgsRq };
                qbxml.Items = new object[1];
                var qbMsgsRq = new QBXMLMsgsRq();
                qbMsgsRq.Items = new object[1];
                qbMsgsRq.onError = QBXMLMsgsRqOnError.stopOnError;

                Cursor.Current = Cursors.WaitCursor;

                try
                {
                    StatusLabel.Text = "Receiving jobs...";
                    TransferJobs(serializer, ns, qbxml, qbMsgsRq);

                    StatusLabel.Text = "Receiving vendors...";
                    TransferVendors(serializer, ns, qbxml, qbMsgsRq);

                    StatusLabel.Text = "Receiving terms...";
                    TransferTerms(serializer, ns, qbxml, qbMsgsRq);

                    if (DEBUGMODE)
                    {
                        StatusLabel.Text = "Receiving accounts...";
                        TransferAccounts(serializer, ns, qbxml, qbMsgsRq);

                        StatusLabel.Text = "Receiving classes...";
                        TransferClasses(serializer, ns, qbxml, qbMsgsRq);
                    }
                }
                finally
                {
                    StatusLabel.Text = "";
                    Cursor.Current = Cursors.Default;
                }

                MessageBox.Show("QuickBooks data has been imported");
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                MessageBox.Show("COM Error Description = " + ex.Message, "COM error");
                return;
            }
            finally
            {
                StatusLabel.Text = "";

                if (_Ticket != null)
                {
                    _Rp.EndSession(_Ticket);
                }
                if (_Rp != null)
                {
                    _Rp.CloseConnection();
                }
            };
        }

        private string LoadField(object fieldvalue, int maxlength)
        {
            //Truncate fields explicitly when realistic and necessary to avoid exceptions
            try
            {
                string value = fieldvalue.ToString();
                if (value.Length > maxlength) value = value.Substring(0, maxlength);
                return value;
            }
            catch
            {
                return null;
            }
        }

        private void TransferClasses(XmlSerializer serializer, XmlSerializerNamespaces ns, QBXML qbxml, QBXMLMsgsRq qbMsgsRq)
        {
            //This is only useful in DEBUGMODE to look at the classes returned by QuickBooks in an XML File
            MemoryStream ms;
            StreamReader sr;
            string xmlRequest;

            var classrq = new ClassQueryRqType();
            classrq.requestID = "1";
            qbMsgsRq.Items[0] = classrq;
            qbxml.Items[0] = qbMsgsRq;

            ms = new MemoryStream();
            serializer.Serialize(ms, qbxml, ns);
            ms.Seek(0, SeekOrigin.Begin);
            sr = new StreamReader(ms);
            xmlRequest = sr.ReadToEnd();
            xmlRequest = xmlRequest.Replace("<?xml version=\"1.0\"?>", "<?xml version=\"1.0\"?><?qbxml version=\"4.0\"?>");
            if (DEBUGMODE) File.WriteAllText("c:\\QB\\ClassQBXML.xml", xmlRequest);
            _Response = _Rp.ProcessRequest(_Ticket, xmlRequest);
            if (DEBUGMODE) File.WriteAllText("c:\\QB\\Classes.xml", _Response);
        }

        private void TransferAccounts(XmlSerializer serializer, XmlSerializerNamespaces ns, QBXML qbxml, QBXMLMsgsRq qbMsgsRq)
        {
            //This is only useful in DEBUGMODE to look at the accounts returned by QuickBooks in an XML File
            MemoryStream ms;
            StreamReader sr;
            string xmlRequest;

            var accountrq = new AccountQueryRqType();
            accountrq.requestID = "1";
            qbMsgsRq.Items[0] = accountrq;
            qbxml.Items[0] = qbMsgsRq;

            ms = new MemoryStream();
            serializer.Serialize(ms, qbxml, ns);
            ms.Seek(0, SeekOrigin.Begin);
            sr = new StreamReader(ms);
            xmlRequest = sr.ReadToEnd();
            xmlRequest = xmlRequest.Replace("<?xml version=\"1.0\"?>", "<?xml version=\"1.0\"?><?qbxml version=\"4.0\"?>");
            if (DEBUGMODE) File.WriteAllText("c:\\QB\\AccountQBXML.xml", xmlRequest);
            _Response = _Rp.ProcessRequest(_Ticket, xmlRequest);
            if (DEBUGMODE) File.WriteAllText("c:\\QB\\Accounts.xml", _Response);
        }

        private void TransferTerms(XmlSerializer serializer, XmlSerializerNamespaces ns, QBXML qbxml, QBXMLMsgsRq qbMsgsRq)
        {
            MemoryStream ms;
            StreamReader sr;
            string xmlRequest;

            var termsrq = new TermsQueryRqType();
            termsrq.requestID = "1";
            qbMsgsRq.Items[0] = termsrq;
            qbxml.Items[0] = qbMsgsRq;

            ms = new MemoryStream();
            serializer.Serialize(ms, qbxml, ns);
            ms.Seek(0, SeekOrigin.Begin);
            sr = new StreamReader(ms);
            xmlRequest = sr.ReadToEnd();
            xmlRequest = xmlRequest.Replace("<?xml version=\"1.0\"?>", "<?xml version=\"1.0\"?><?qbxml version=\"4.0\"?>");
            if (DEBUGMODE) File.WriteAllText("c:\\QB\\TermsQBXML.xml", xmlRequest);
            _Response = _Rp.ProcessRequest(_Ticket, xmlRequest);
            if (DEBUGMODE) File.WriteAllText("c:\\QB\\Terms.xml", _Response);

            QBXML rsXML = GetQbxml(serializer);

            if (rsXML?.Items?[0] is QBXMLMsgsRs)
            {
                QBXMLMsgsRs msgsrs = (QBXMLMsgsRs)rsXML.Items[0];
                if (msgsrs?.Items?[0] is TermsQueryRsType)
                {
                    TermsQueryRsType rs = (TermsQueryRsType)msgsrs.Items[0];
                    using (ApexDataDataContext dc = new ApexDataDataContext(_SqlConnBuilder.ConnectionString))
                    {
                        foreach (var term in rs.Items)
                        {
                            if (term is StandardTermsRet)
                            {
                                StandardTermsRet qbTerm = (StandardTermsRet)term;
                                if (!dc.VENDTERMs.Where(s => s.VendTerm1 == qbTerm.Name).Any())
                                {
                                    VENDTERM newTerm = new VENDTERM();
                                    newTerm.VendTerm1 = qbTerm.Name;
                                    dc.VENDTERMs.InsertOnSubmit(newTerm);
                                }
                            }
                            dc.SubmitChanges();
                        }
                    }
                }
            }
        }

        private string GetNextApexVendor(ApexDataDataContext dc)
        {
            const string STARTVENDORID = "10000";

            string lastVendorID = String.Empty;
            try
            {
                lastVendorID = dc.QBVendors
                   .Where(s => s.ApexCompany.StartsWith(_ApexTargetCompany))
                   .Select(s => s.ApexVendorID).Max();
            }
            catch
            {
            }

            if (string.IsNullOrEmpty(lastVendorID))
            {
                lastVendorID = _ApexTargetCompany + STARTVENDORID;
            }

            int id;
            bool parseOK = Int32.TryParse(lastVendorID.Substring(1, 5), out id);
            if (parseOK)
                return _ApexTargetCompany + (id + 1).ToString();
            else
                return _ApexTargetCompany + STARTVENDORID;
        }

        private void TransferVendors(XmlSerializer serializer, XmlSerializerNamespaces ns, QBXML qbxml, QBXMLMsgsRq qbMsgsRq)
        {
            MemoryStream ms;
            StreamReader sr;
            string xmlRequest;

            var vendrq = new VendorQueryRqType();
            vendrq.requestID = "1";
            qbMsgsRq.Items[0] = vendrq;
            qbxml.Items[0] = qbMsgsRq;
            ms = new MemoryStream();
            serializer.Serialize(ms, qbxml, ns);
            ms.Seek(0, SeekOrigin.Begin);
            sr = new StreamReader(ms);
            xmlRequest = sr.ReadToEnd();
            xmlRequest = xmlRequest.Replace("<?xml version=\"1.0\"?>", "<?xml version=\"1.0\"?><?qbxml version=\"4.0\"?>");
            if (DEBUGMODE) File.WriteAllText("c:\\QB\\VendQBXML.xml", xmlRequest);
            _Response = _Rp.ProcessRequest(_Ticket, xmlRequest);
            if (DEBUGMODE) File.WriteAllText("c:\\QB\\Vendors.xml", _Response);

            QBXML rsXML = GetQbxml(serializer);

            if (rsXML?.Items?[0] is QBXMLMsgsRs)
            {
                QBXMLMsgsRs msgsrs = (QBXMLMsgsRs)rsXML.Items[0];
                if (msgsrs?.Items?[0] is VendorQueryRsType)
                {
                    VendorQueryRsType rs = (VendorQueryRsType)msgsrs.Items[0];

                    if (rs.statusCode != "0")
                    {
                        MessageBox.Show(rs.statusMessage);
                    }
                    else
                    {
                        for (int i = 0; i < rs.VendorRet.Length; i++)
                        {
                            VendorRet vr = rs.VendorRet[i];

                            using (ApexDataDataContext dc = new ApexDataDataContext(_SqlConnBuilder.ConnectionString))
                            {
                                Vendor vendor = null;
                                QBVendor qbvendor = dc.QBVendors
                                   .Where(s => s.ApexCompany == _ApexTargetCompany && s.QBListID == vr.ListID)
                                   .SingleOrDefault();
                                bool newRecord;

                                if (qbvendor == null) //new vendor
                                {
                                    newRecord = true;
                                    qbvendor = new QBVendor();
                                    vendor = new Vendor();

                                    string newVendorID = GetNextApexVendor(dc);

                                    //Set up the translation table
                                    qbvendor.QBListID = vr?.ListID;
                                    qbvendor.QBVendorName = vr?.Name;
                                    qbvendor.ApexVendorID = newVendorID.PadLeft(6);  //pad it just in case we change the way we're numbering
                                    qbvendor.ApexCompany = _ApexTargetCompany;
                                    qbvendor.Terms = vr?.TermsRef?.FullName;

                                    //Start the new Apex vendor
                                    vendor.Vendor1 = newVendorID;
                                    vendor.AcctID = newVendorID;
                                    vendor.Name = LoadField(vr?.Name.ToUpper(), 25);
                                }
                                else
                                {
                                    newRecord = false;
                                    qbvendor.Terms = vr?.TermsRef?.FullName;
                                    vendor = dc.Vendors.Where(s => s.Vendor1 == qbvendor.ApexVendorID).Single();
                                }

                                vendor.Add1 = LoadField(vr?.VendorAddress?.Addr1, 25);
                                vendor.Add2 = LoadField(vr?.VendorAddress?.Addr2, 25);
                                vendor.City = LoadField(vr?.VendorAddress?.City, 15);
                                vendor.State = LoadField(vr?.VendorAddress?.State, 4);
                                vendor.Zip = LoadField(vr?.VendorAddress?.PostalCode, 15);
                                vendor.EMail = LoadField(vr?.Email, 40);
                                vendor.Attn = LoadField(vr?.Contact, 20);
                                vendor.Phone = LoadField(vr?.Phone, 15);
                                vendor.CompLevel = 0;
                                vendor.Terms = LoadField(vr?.TermsRef?.FullName, 15);
                                vendor.Company = _ApexTargetCompany;

                                if (newRecord)
                                {
                                    dc.QBVendors.InsertOnSubmit(qbvendor);
                                    dc.Vendors.InsertOnSubmit(vendor);
                                }
                                dc.SubmitChanges();
                            }
                        }
                    }
                }
            }
        }

        private void TransferJobs(XmlSerializer serializer, XmlSerializerNamespaces ns, QBXML qbxml, QBXMLMsgsRq qbMsgsRq)
        {
            MemoryStream ms;
            StreamReader sr;
            string xmlRequest;

            var custrq = new CustomerQueryRqType();
            custrq.requestID = "1";
            qbMsgsRq.Items = new object[1] { custrq };
            qbxml.Items[0] = qbMsgsRq;
            ms = new MemoryStream();
            serializer.Serialize(ms, qbxml, ns);
            ms.Seek(0, SeekOrigin.Begin);
            sr = new StreamReader(ms);
            xmlRequest = sr.ReadToEnd();
            xmlRequest = xmlRequest.Replace("<?xml version=\"1.0\"?>", "<?xml version=\"1.0\"?><?qbxml version=\"4.0\"?>");
            if (DEBUGMODE) File.WriteAllText("c:\\QB\\CustQBXML.xml", xmlRequest);
            _Response = _Rp.ProcessRequest(_Ticket, xmlRequest);
            if (DEBUGMODE) File.WriteAllText("c:\\QB\\Customers.xml", _Response);

            QBXML rsXML = GetQbxml(serializer);

            if (rsXML?.Items?[0] is QBXMLMsgsRs)
            {
                QBXMLMsgsRs msgsrs = (QBXMLMsgsRs)rsXML.Items[0];
                if (msgsrs?.Items?[0] is CustomerQueryRsType)
                {
                    CustomerQueryRsType rs = (CustomerQueryRsType)msgsrs.Items[0];

                    if (rs.statusCode != "0")
                    {
                        MessageBox.Show(rs.statusMessage);
                    }
                    else
                    {
                        for (int i = 0; i < rs.CustomerRet.Length; i++)
                        {
                            CustomerRet cr = rs.CustomerRet[i];

                            if (cr.Sublevel == "0") continue;  //don't process the top level customers; we only want the job records

                            Match m = _JobRegEx.Match(cr?.FullName);

                            if (!m.Success) continue;  //this isn't a job number we can use

                            string qbJobID = m.Groups[1].Value;
                            string qbJobName = m.Groups[2].Value;

                            using (ApexDataDataContext dc = new ApexDataDataContext(_SqlConnBuilder.ConnectionString))
                            {
                                Job job = null;
                                QBJob qbjob = dc.QBJobs.Where(s => s.ApexCompany == _ApexTargetCompany && s.QBListID == cr.ListID).SingleOrDefault();

                                if (qbjob != null)
                                {
                                    bool validJob = dc.Jobs.Where(s => s.Job1 == qbjob.ApexJobID).Any();
                                    if (!validJob)  //Clean up records if an Apex user has changed or deleted the job (Test results 9/26/2017)
                                    {
                                        dc.QBJobs.DeleteOnSubmit(qbjob);
                                        dc.SubmitChanges();
                                        qbjob = null;
                                    }

                                }

                                bool newRecord;

                                if (qbjob == null) //new job
                                {
                                    newRecord = true;
                                    qbjob = new QBJob();
                                    job = new Job();

                                    //Set up the translation table
                                    qbjob.QBListID = cr?.ListID;
                                    qbjob.QBJobName = cr?.FullName;
                                    qbjob.ApexJobID = qbJobID.PadLeft(12);
                                    qbjob.ApexCompany = _ApexTargetCompany;

                                    //Start the new Apex job
                                    job.Job1 = qbJobID.PadLeft(12);
                                    job.Act = "A";
                                    job.Schedule = "STD";
                                    job.TaxDefault = "Y";
                                    job.TaxRate = 0;
                                    job.POMsg = String.Empty;
                                }
                                else
                                {
                                    newRecord = false;
                                    job = dc.Jobs.Where(s => s.Job1 == qbjob.ApexJobID).Single();
                                }

                                job.Add1 = LoadField(cr?.ShipAddress?.Addr1, 25);
                                job.Add2 = LoadField(cr?.ShipAddress?.Addr2, 25);
                                job.City = LoadField(cr?.ShipAddress?.City, 15);
                                job.State = LoadField(cr?.ShipAddress?.State, 4);
                                job.Zip = LoadField(cr?.ShipAddress?.PostalCode, 15);
                                job.EMail = LoadField(cr?.Email, 40);
                                job.Attn = LoadField(cr?.Contact, 20);
                                job.Phone = LoadField(cr?.Phone, 15);
                                job.Company = _ApexTargetCompany;
                                job.Name = LoadField(qbJobName, 25);

                                if (newRecord)
                                {
                                    dc.QBJobs.InsertOnSubmit(qbjob);
                                    dc.Jobs.InsertOnSubmit(job);
                                }
                                dc.SubmitChanges();
                            }
                        }
                    }
                }
            }
        }
    }
}

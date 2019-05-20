using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApexQB
{
    public static class GLAcctUtility
    {
        public struct GLAcct
        {
            public string ApexCompany { get; set; }
            public string ApexGLRef { get; set; }
            public string QBGLAcctFullName { get; set; }
        }

        public static List<GLAcct> GLAcctList;

        public static List<GLAcct> BuildGLAcctList(ApexDataDataContext apexData)
        {
            //This has now been standardized between companies, and is in the Apex property bag
            //Due to the number of changes that we have made to date, I am retaining the design where this can vary again by company

            List<GLAcct> acctList = new List<GLAcct>();

            string acctName = apexData.PropBags.Where(s => s.PropName == "ApexQB-M").Select(s => s.PropVal).FirstOrDefault();
            if (acctName == null) throw new Exception("ApexQB G/L M property is not present, process aborting");
            acctList.Add(new GLAcct { ApexCompany = "M", ApexGLRef = "M", QBGLAcctFullName = acctName });
            acctList.Add(new GLAcct { ApexCompany = "T", ApexGLRef = "M", QBGLAcctFullName = acctName });
            acctList.Add(new GLAcct { ApexCompany = "E", ApexGLRef = "M", QBGLAcctFullName = acctName });

            acctName = apexData.PropBags.Where(s => s.PropName == "ApexQB-R").Select(s => s.PropVal).FirstOrDefault();
            if (acctName == null) throw new Exception("ApexQB G/L R property is not present, process aborting");
            acctList.Add(new GLAcct { ApexCompany = "M", ApexGLRef = "R", QBGLAcctFullName = acctName });
            acctList.Add(new GLAcct { ApexCompany = "T", ApexGLRef = "R", QBGLAcctFullName = acctName });
            acctList.Add(new GLAcct { ApexCompany = "E", ApexGLRef = "R", QBGLAcctFullName = acctName });

            acctName = apexData.PropBags.Where(s => s.PropName == "ApexQB-T").Select(s => s.PropVal).FirstOrDefault();
            if (acctName == null) throw new Exception("ApexQB G/L T property is not present, process aborting");
            acctList.Add(new GLAcct { ApexCompany = "M", ApexGLRef = "T", QBGLAcctFullName = acctName });
            acctList.Add(new GLAcct { ApexCompany = "T", ApexGLRef = "T", QBGLAcctFullName = acctName });
            acctList.Add(new GLAcct { ApexCompany = "E", ApexGLRef = "T", QBGLAcctFullName = acctName });

            acctName = apexData.PropBags.Where(s => s.PropName == "ApexQB-S").Select(s => s.PropVal).FirstOrDefault();
            if (acctName == null) throw new Exception("ApexQB G/L S property is not present, process aborting");
            acctList.Add(new GLAcct { ApexCompany = "M", ApexGLRef = "S", QBGLAcctFullName = acctName });
            acctList.Add(new GLAcct { ApexCompany = "T", ApexGLRef = "S", QBGLAcctFullName = acctName });
            acctList.Add(new GLAcct { ApexCompany = "E", ApexGLRef = "S", QBGLAcctFullName = acctName });

            acctName = apexData.PropBags.Where(s => s.PropName == "ApexQB-E").Select(s => s.PropVal).FirstOrDefault();
            if (acctName == null) throw new Exception("ApexQB G/L E property is not present, process aborting");
            acctList.Add(new GLAcct { ApexCompany = "M", ApexGLRef = "E", QBGLAcctFullName = acctName });
            acctList.Add(new GLAcct { ApexCompany = "T", ApexGLRef = "E", QBGLAcctFullName = acctName });
            acctList.Add(new GLAcct { ApexCompany = "E", ApexGLRef = "E", QBGLAcctFullName = acctName });

            return acctList;

        //    return new List<GLAcct>
        // {
        //    //CEM, company "M"
        //    new GLAcct { ApexCompany = "M", ApexGLRef = "M", QBGLAcctFullName = "Cost of Goods Sold:MATERIALS:JOB MATERIALS - PO" },
        //    new GLAcct { ApexCompany = "M", ApexGLRef = "R", QBGLAcctFullName = "Cost of Goods Sold:RENTAL EQUIPMENT:Rental Equipment" },
        //    new GLAcct { ApexCompany = "M", ApexGLRef = "T", QBGLAcctFullName = "Cost of Goods Sold:RENTAL EQUIPMENT:Trailer Rental" },
        //    new GLAcct { ApexCompany = "M", ApexGLRef = "S", QBGLAcctFullName = "Cost of Goods Sold:SUBCONTRACTORS EXPENSE" },
        //    new GLAcct { ApexCompany = "M", ApexGLRef = "E", QBGLAcctFullName = "Cost of Goods Sold:SMALL Tools & Repairs PO" },

        //    //CET, company "T"
        //    new GLAcct { ApexCompany = "T", ApexGLRef = "M", QBGLAcctFullName = "Cost of Goods Sold (Job Cost):H MATERIALS:15 Materials PO" },
        //    new GLAcct { ApexCompany = "T", ApexGLRef = "R", QBGLAcctFullName = "Cost of Goods Sold (Job Cost):F RENTAL EQUIPMENT:27 Rental Equipment" },
        //    new GLAcct { ApexCompany = "T", ApexGLRef = "T", QBGLAcctFullName = "Cost of Goods Sold (Job Cost):F RENTAL EQUIPMENT:40 Trailer Rental" },
        //    new GLAcct { ApexCompany = "T", ApexGLRef = "S", QBGLAcctFullName = "Cost of Goods Sold (Job Cost):I SUBCONTRACTS" },
        //    new GLAcct { ApexCompany = "T", ApexGLRef = "E", QBGLAcctFullName = "Cost of Goods Sold (Job Cost):E TOOLS AND EQUIPMENT:35 Small Tools & Repairs PO" },

        //    //CEDB, company "E"
        //    new GLAcct { ApexCompany = "E", ApexGLRef = "M", QBGLAcctFullName = "Cost of Goods Sold (Job Cost):H MATERIALS:15 Materials PO" },
        //    new GLAcct { ApexCompany = "E", ApexGLRef = "R", QBGLAcctFullName = "Cost of Goods Sold (Job Cost):F RENTAL EQUIPMENT:27 Rental Equipment" },
        //    new GLAcct { ApexCompany = "E", ApexGLRef = "T", QBGLAcctFullName = "Cost of Goods Sold (Job Cost):F RENTAL EQUIPMENT:40 Trailer Rental" },
        //    new GLAcct { ApexCompany = "E", ApexGLRef = "S", QBGLAcctFullName = "Cost of Goods Sold (Job Cost):I SUBCONTRACTS" },
        //    new GLAcct { ApexCompany = "E", ApexGLRef = "E", QBGLAcctFullName = "Cost of Goods Sold (Job Cost):E TOOLS AND EQUIPMENT:35 Small Tools & Repairs PO" }
        // };
        }
    }
}

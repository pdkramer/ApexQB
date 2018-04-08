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

      public static List<GLAcct> BuildGLAcctList()
      {
         return new List<GLAcct>
         {
            //CEM, company "M"
            new GLAcct { ApexCompany = "M", ApexGLRef = "M", QBGLAcctFullName = "Cost of Goods Sold:MATERIALS:JOB MATERIALS - PO" },
            new GLAcct { ApexCompany = "M", ApexGLRef = "R", QBGLAcctFullName = "Cost of Goods Sold:RENTAL EQUIPMENT:Rental Equipment" },
            new GLAcct { ApexCompany = "M", ApexGLRef = "T", QBGLAcctFullName = "Cost of Goods Sold:RENTAL EQUIPMENT:Trailer Rental" },
            new GLAcct { ApexCompany = "M", ApexGLRef = "S", QBGLAcctFullName = "Cost of Goods Sold:SUBCONTRACTORS EXPENSE" },
            new GLAcct { ApexCompany = "M", ApexGLRef = "E", QBGLAcctFullName = "Cost of Goods Sold:SMALL Tools & Repairs PO" },

            //CET, company "T"
            new GLAcct { ApexCompany = "T", ApexGLRef = "M", QBGLAcctFullName = "Cost of Goods Sold:H - MATERIALS:Materials - PO" },
            new GLAcct { ApexCompany = "T", ApexGLRef = "R", QBGLAcctFullName = "Cost of Goods Sold:F - RENTAL EQUIPMENT:Rental Equipment - PO" },
            new GLAcct { ApexCompany = "T", ApexGLRef = "T", QBGLAcctFullName = "Cost of Goods Sold:F - RENTAL EQUIPMENT:Trailer Rental" },
            new GLAcct { ApexCompany = "T", ApexGLRef = "S", QBGLAcctFullName = "Cost of Goods Sold:I - SUBCONTRACTS:Subcontracts" },
            new GLAcct { ApexCompany = "T", ApexGLRef = "E", QBGLAcctFullName = "Cost of Goods Sold:E - TOOLS AND EQUIPMENT:Small Tools & Repairs PO" },

            //CEDB, company "E"
            new GLAcct { ApexCompany = "E", ApexGLRef = "M", QBGLAcctFullName = "Cost of Goods Sold (Job Cost):H - MATERIALS:15 - Materials PO" },
            new GLAcct { ApexCompany = "E", ApexGLRef = "R", QBGLAcctFullName = "Cost of Goods Sold (Job Cost):F - RENTAL EQUIPMENT:27 - Rental Equipment" },
            new GLAcct { ApexCompany = "E", ApexGLRef = "T", QBGLAcctFullName = "Cost of Goods Sold (Job Cost):I - SUBCONTRACTS:18 - Subcontracts" },
            new GLAcct { ApexCompany = "E", ApexGLRef = "S", QBGLAcctFullName = "Cost of Goods Sold (Job Cost):E - TOOLS AND EQUIPMENT:Small Tools & Repairs PO" },
            new GLAcct { ApexCompany = "E", ApexGLRef = "E", QBGLAcctFullName = "Cost of Goods Sold (Job Cost):F - RENTAL EQUIPMENT:40 - Trailer Rental" }
         };
      }
   }
}

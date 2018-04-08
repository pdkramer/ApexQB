namespace ApexQB
{
    //These are the lines for the QuickBooks invoice transfer status report
   public struct StatusLine
    {
        public string Invoice { get; set; }
        public string PO { get; set; }
        public string StatusCode { get; set; }
        public string Message { get; set; }
    }
}

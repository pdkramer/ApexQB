# ApexQB
### Apex/QuickBooks Interface

This application provides an interface between Apex (Advanced Purchasing Extensions, written by Vulcan Software LLC) and QuickBooks.

Unfortunately, it cannot be run independently since it requires an Apex database.  

It does, however, show a development approach that might be interesting to QuickBooks interface developers.  I wanted the flexibility and control of using qbXML but with a higher level of abstraction.  I did not want to use QBFC because I did not want the client to have to be concerned about distributing additional files to each user’s computer.

To that end I generated C# classes based on the QuickBooks XSD files that you can find at C:\Program Files (x86)\Intuit\IDN\Common\Tools\Validator.  The result is the 118,983 line file qbxmlops130.cs.  Using these classes I can serialize and deserialize the QuickBooks XML requests and responses using a straightforward syntax.  The resulting XML files are by definition syntactically valid since they are generated using the XSD files that Intuit uses for their own validator tool.

-- *Phil*

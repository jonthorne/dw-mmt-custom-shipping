using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dynamicweb.MMT.Custom.Shipping.Models
{
    public class BCTempWebOrder
    {
        public string Type { get; set; }
        public string Order_No { get; set; }
        public string? Customer_No { get; set; }
        public string? Ship_to_Name { get; set; }
        public string? Ship_to_Contact_Name { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Post_code { get; set; }
        public string? Country { get; set; }
        public string? Phone_No { get; set; }
        public string? Item_No { get; set; }
        public double? Quantity { get; set; }
    }
}

/*
{
    "@odata.context": "https://api.businesscentral.dynamics.com/v2.0/d3dc8acb-7d3c-4de5-9da7-095c210abcbd/MMTWiise-Training/ODataV4/$metadata#Company('MMT%20AU')/TempWebOrder/$entity",
    "@odata.etag": "W/\"JzE5OzY1NDMxNDM3NDgyOTI5NjQ2MzYxOzAwOyc=\"",
    "Entry_No": 20000,
    "Type": "Header",
    "Order_No": "TESTING100",
    "Customer_No": "101TG000",
    "Ship_to_Name": "test1",
    "Ship_to_Contact_Name": "test1",
    "Address": "193-195 Springvale Rd",
    "City": "NUNAWADING",
    "State": "VIC",
    "Post_code": "3131",
    "Country": "AU",
    "Phone_No": "99991000",
    "Item_No": "",
    "Quantity": 0
}
*/

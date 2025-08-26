using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dynamicweb.MMT.Custom.Shipping.Models
{
    public class BCWebOrderFreightQuote
    {
        public string Licence_No { get; set; }
        public string ID { get; set; }
        public string? Order_No { get; set; }
        public string? Carrier_Name { get; set; }
        public string? Service { get; set; }
        public double Rate_Cost { get; set; }
        public double Rate_Price { get; set; }
        public string? Shipping_Agent_Code { get; set; }
        public string? Shipping_Agent_Service_Code { get; set; }
        public string? Delivery_Date { get; set; }
    }
}

/*
{
            "@odata.etag": "W/\"JzIwOzEyNjE0OTk0NTEyNTkyMTIwNDQzMTswMDsn\"",
            "Licence_No": "LPQ0001",
            "ID": "Mach-513-3712-27799",
            "Order_No": "TESTING100",
            "Carrier_Name": "Australia Post (AUSPOST)",
            "Service": "EXPRESS POST + SIGNATURE",
            "Rate_Cost": 95.93,
            "Rate_Price": 115.12,
            "Shipping_Agent_Code": "AUSPOST",
            "Shipping_Agent_Service_Code": "EXPS",
            "Delivery_Date": "2025-08-18T23:59:59Z"
        },
        */



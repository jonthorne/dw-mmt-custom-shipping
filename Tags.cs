using System;
using System.Linq;
using System.Globalization;

using Dynamicweb.Ecommerce.Orders;
using Dynamicweb.Ecommerce.Prices;
using Dynamicweb.Rendering;
using Dynamicweb.Ecommerce;

using Dynamicweb.MMT.Custom.Shipping.Models;

namespace Dynamicweb.MMT.Custom.Shipping
{
    internal static class Tags
    {
        #region Common tags
        public const string CustomerAddress = "Order.Customer.Address";
        public const string CustomerAddress2 = "Order.Customer.Address2";
        public const string CustomerCity = "Order.Customer.City";
        public const string CustomerCountry = "Order.Customer.Country";
        public const string CustomerCountryCode = "Order.Customer.Country.Code";
        public const string CustomerRegion = "Order.Customer.Region";
        public const string CustomerZip = "Order.Customer.Zip";
        public const string CustomerZipCode = "Order.Customer.ZipCode";

        public const string DeliveryAddress = "Order.Delivery.Address";
        public const string DeliveryAddress2 = "Order.Delivery.Address2";
        public const string DeliveryCity = "Order.Delivery.City";
        public const string DeliveryCountry = "Order.Delivery.Country";
        public const string DeliveryCountryCode = "Order.Delivery.Country.Code";
        public const string DeliveryRegion = "Order.Delivery.Region";
        public const string DeliveryZip = "Order.Delivery.Zip";
        public const string DeliveryZipCode = "Order.Delivery.ZipCode";

        #endregion

        #region Shipment Services (aka "Carrier")
        public const string IsSelectedCarrier = "IsSelectedCarrier";

        public const string ShippingServices = "ShippingServices";

        public const string ServiceCarrierName = "ServiceCarrierName";
        public const string AgentCode = "AgentCode";
        public const string AgentServiceCode = "AgentServiceCode";
        public const string ServiceDescription = "ServiceDescription";
        public const string ServicePrice = "ServicePrice";
        public const string DeliveryTime = "DeliveryTime";
        #endregion

        #region Drop points
        public const string IsSelectedServicePoint = "IsSelectedServicePoint";

        public const string DropPointsLoop = "DropPoints";

        public const string Number = "Number";
        public const string Id = "Id";
        public const string CompanyName = "CompanyName";
        public const string Name = "Name";
        public const string Address = "Address";
        public const string Address2 = "Address2";
        public const string Zipcode = "Zipcode";
        public const string City = "City";
        public const string Country = "Country";
        public const string Distance = "Distance";
        public const string Longitude = "Longitude";
        public const string Latitude = "Latitude";
        public const string Agent = "Agent";
        public const string CarrierCode = "CarrierCode";
        public const string OpeningHours = "OpeningHours";
        public const string InDelivery = "InDelivery";
        public const string OutDelivery = "OutDelivery";
        public const string ServicePointRequired = "ServicePointRequired";
        #endregion

        public static void SetMainTags(Template template, Order order)
        {
            template.SetTag(CustomerAddress, order.CustomerAddress);
            template.SetTag(CustomerAddress2, order.CustomerAddress2);
            template.SetTag(CustomerCity, order.CustomerCity);
            template.SetTag(CustomerCountry, order.CustomerCountry);
            template.SetTag(CustomerCountryCode, order.CustomerCountryCode);
            template.SetTag(CustomerRegion, order.CustomerRegion);
            template.SetTag(CustomerZip, order.CustomerZip);
            template.SetTag(CustomerZipCode, order.CustomerZip);

            template.SetTag(DeliveryAddress, order.DeliveryAddress);
            template.SetTag(DeliveryAddress2, order.DeliveryAddress2);
            template.SetTag(DeliveryCity, order.DeliveryCity);
            template.SetTag(DeliveryCountry, order.DeliveryCountry);
            template.SetTag(DeliveryCountryCode, order.DeliveryCountryCode);
            template.SetTag(DeliveryRegion, order.DeliveryRegion);
            template.SetTag(DeliveryZip, order.DeliveryZip);
            template.SetTag(DeliveryZipCode, order.DeliveryZip);
        }

        public static void SetServiceTags(Template servicesLoop, BCWebOrderFreightQuote rate, bool isSelected, Order order)
        {
            servicesLoop.SetTag(IsSelectedCarrier, isSelected);
            servicesLoop.SetTag(ServiceCarrierName, rate.Carrier_Name);
            servicesLoop.SetTag(ServiceDescription, rate.Service);
            servicesLoop.SetTag(AgentCode, rate.Shipping_Agent_Code);
            servicesLoop.SetTag(AgentServiceCode, rate.Shipping_Agent_Service_Code);
            servicesLoop.SetTag("ServiceId", rate.ID);

            var currency = Services.Currencies.GetCurrency(order.CurrencyCode);
            if (currency is null)
            {
                throw new Exception($"Currency code {order.CurrencyCode} does not exists.");
            }
            PriceInfo price = new PriceInfo(currency)
            {
                PriceWithoutVAT = rate.Rate_Price,
                PriceWithVAT = rate.Rate_Price,//(rate.Rate_Price * 1.1),
                VAT = 0,//VAT = (rate.Rate_Price * 1.1) - rate.Rate_Price,
                IsInformative = true
            };
            price = price.ToPrice(order.Currency);
            Ecommerce.Frontend.Renderer.RenderPriceInfo(price, servicesLoop, ServicePrice);

            try
            {
                DateTime targetDate = DateTime.ParseExact(
                    rate.Delivery_Date,
                    "MM/dd/yyyy HH:mm:ss",
                    CultureInfo.InvariantCulture
                );
                TimeSpan diff = targetDate - DateTime.Now;
                double days = diff.TotalDays;
                servicesLoop.SetTag(DeliveryTime, $"{Math.Ceiling(days).ToString()} days");
            }
            catch { }

            servicesLoop.CommitLoop();
        }

    }
}

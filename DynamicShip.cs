using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.Json;

using Dynamicweb.Ecommerce.International;
using Dynamicweb.Ecommerce.Cart;
using Dynamicweb.Ecommerce.Orders;
using Dynamicweb.Ecommerce.Prices;
using Dynamicweb.Ecommerce.Products;
using Dynamicweb.Extensibility.AddIns;
using Dynamicweb.Extensibility.Editors;
using Dynamicweb.Rendering;
using Dynamicweb.DataIntegration.EndpointManagement;
using Dynamicweb.Logging;
using Dynamicweb.Security.UserManagement;

using Dynamicweb.MMT.Custom.Shipping.Helpers;
using Dynamicweb.MMT.Custom.Shipping.Models;

namespace Dynamicweb.MMT.Custom.Shipping
{
    /// <summary>
    /// DynamicShip Shipping Service
    /// </summary>
    [AddInName("DynamicShip"), AddInDescription("DynamicShip Shipping Provider")]
    public class DynamicShip : ShippingProvider
    {
        #region Parameters

        [AddInLabel("Shipping Provider Template"), AddInParameter("ShippingProviderTemplate"), AddInParameterEditor(typeof(TemplateParameterEditor), "folder=Templates/eCom7/ShippingProvider")]
        public string ShippingProviderTemplate { get; set; }

        [AddInParameter("Create Debug Log"), AddInParameterEditor(typeof(YesNoParameterEditor), ""), AddInDescription("Create a log of the request and response from BorderExpress")]
        public bool DebugLog { get; set; }

        #endregion

        ILogger _logger = null;

        /// <summary>
        /// Default constructor
        /// </summary>
        public DynamicShip()
        {
            _logger = LogManager.Current.GetLogger("Custom/DynamicShip");
        }

        /// <summary>
        /// Calculate shipping fee for the specified order
        /// </summary>
        /// <param name="order">The order.</param>
        /// <returns>Returns shipping fee for the specified order</returns>
        public override PriceRaw CalculateShippingFee(Order order)
        {
            double shippingRate = 0;
            order.ShippingProviderErrors.Clear();
            order.ShippingProviderWarnings.Clear();
            try
            {
                string dynamicShipServiceId = string.Empty;
                if (order.ShippingProviderValues.TryGetValue("DynamicShipServiceId", out object serviceId))
                {
                    dynamicShipServiceId = serviceId.ToString();
                    if (DebugLog) _logger.Info("Service selectd: " + dynamicShipServiceId);
                }
                else
                {
                    order.ShippingProviderErrors.Add("Shipping service not selected.");
                    if (DebugLog) _logger.Info("Shipping service not selected.");
                    return new PriceRaw(0, order.Currency);
                }

                var rates = GetShippingRates(order);

                BCWebOrderFreightQuote selectShippingRate = null;
                foreach (var rateJson in rates)
                {
                    var rateObject = JsonSerializer.Deserialize<BCWebOrderFreightQuote>(rateJson);
                    if (rateObject.ID == dynamicShipServiceId)
                    {
                        if (DebugLog) _logger.Info($"Rate found: {rateJson}:");
                        selectShippingRate = rateObject;
                        order.ShippingMethod = rateObject.Carrier_Name + " - " + rateObject.Service;
                        order.ShippingMethodAgentCode = rateObject.Shipping_Agent_Code;
                        order.ShippingMethodAgentServiceCode = rateObject.Shipping_Agent_Service_Code;
                        break;
                    }
                }
                if (selectShippingRate == null)
                {
                    order.ShippingProviderErrors.Add("Shipping rate not found.");
                    if (DebugLog) _logger.Info("Shipping rate not found.");
                    return new PriceRaw(0, order.Currency);
                }

                return new PriceRaw(selectShippingRate.Rate_Price, order.Currency);
            }
            catch (Exception ex)
            {
                order.ShippingProviderErrors.Add(ex.Message);

                if (DebugLog) _logger.Info(ex.Message);
                if (DebugLog) _logger.Info(ex.StackTrace ?? "");
            }
            return new PriceRaw(0, order.Currency);
            //return null;
        }

        public override void ProcessOrder(Order order)
        {
            base.ProcessOrder(order);
        }
        
        public override string RenderFrontend(Order order)
        {
            if (string.IsNullOrEmpty(ShippingProviderTemplate))
            {
                return base.RenderFrontend(order);
            }

            var template = new Template(TemplateHelper.GetTemplatePath(ShippingProviderTemplate, "eCom7/ShippingProvider"));
            if (string.IsNullOrEmpty(template.Html))
            {
                return string.Empty;
            }

            template.SetTag("FieldPrefix", FieldPrefix);
            Tags.SetMainTags(template, order);

            bool firstSelected = false;
            string dynamicShipServiceId = string.Empty;
            if (order.ShippingProviderValues.TryGetValue("DynamicShipServiceId", out object serviceId))
            {
                dynamicShipServiceId = serviceId.ToString();
                if (DebugLog) _logger.Info("Service selectd: " + dynamicShipServiceId);
            }
            else
            {
                firstSelected = true;
            }

            try
            {
                if (template.TagExists(Tags.ShippingServices))
                {
                    var servicesLoop = template.GetLoop(Tags.ShippingServices);

                    var rates = GetShippingRates(order);

                    foreach (var rateJson in rates)
                    {
                        var rateObject = JsonSerializer.Deserialize<BCWebOrderFreightQuote>(rateJson);
                        bool selected = false;
                        if (rateObject.ID == dynamicShipServiceId)
                        {
                            selected = true;
                        }
                        Tags.SetServiceTags(servicesLoop, rateObject, selected, order);
                    }
                }
            }
            catch (Exception ex)
            {
                order.ShippingProviderErrors.Add(ex.Message);

                if (DebugLog) _logger.Info(ex.Message);
                if (DebugLog) _logger.Info(ex.StackTrace);
            }

            return template.Output();
        }
        
        public List<string> GetShippingRates(Order order)
        {
            string cacheKey = order.Id + "_ShippingRates";
            List<string> rates = (List<string>)GetCachedResult(cacheKey);
            if (rates != null && rates.Any())
            {
                if (DebugLog) _logger.Info("Using cached shipping rates.");
                return rates;
            }

            rates = new List<string>();
            try
            {
                CreateTempWebOrderHeader(order);
                CreateTempWebOrderLines(order);
            }
            catch (Exception e)
            {
                throw new Exception("Error creating temp web order: " + e.Message, e);
            }

            try
            {
                var freightList = GetFreightQuote(order);

                if (freightList == null || !freightList.Any())
                {
                    if (DebugLog) _logger.Info("No freight quotes found for order: " + order.Id);
                    throw new Exception("No freight quotes found for order: " + order.Id);
                }

                foreach (var freight in freightList)
                    {
                        rates.Add(JsonSerializer.Serialize(freight));
                    }
            }
            catch (Exception ex)
            {
                throw new Exception("Error getting freight quote: " + ex.Message, ex);
            }

            if (DebugLog) _logger.Info("Retrieved shipping rates: " + string.Join(", ", rates));
            SetCachedResult(cacheKey, rates);
            return rates;
        }
        public void CreateTempWebOrderHeader(Order order)
        {
            //https://api.businesscentral.dynamics.com/v2.0/{{env}}/ODataV4/Company('MMT%20AU')/TempWebOrder

            EndpointService endpointService = new EndpointService();
            Endpoint endpoint = endpointService.GetEndpoints().First(x => x.Name == "TempWebOrder");
            if (endpoint == null) throw new Exception("Endpoint not found: TempWebOrder");
            EndpointHelper endpointDestinationWriter = new EndpointHelper(30, DebugLog ? _logger : null, endpoint.Collection.AuthenticationId);
            string url = endpoint.Url;

            if (DebugLog) _logger.Info("Creating TempWebOrder Header URL: " + url);
            if (DebugLog) _logger.Info("Creating TempWebOrder Header for: " + order.Id);
            UserService userService = new UserService();
            User customer = userService.GetUserById(order.CustomerAccessUserId);
            BCTempWebOrder tempWebOrderHeader = new BCTempWebOrder
            {
                Type = "Header",
                Order_No = order.Id,
                Customer_No = UserContext.Current.User.CustomerNumber,
                Ship_to_Name = string.IsNullOrEmpty(customer.Name) ? "Unknown" : customer.Name,
                Ship_to_Contact_Name = string.IsNullOrEmpty(customer.Name) ? "Unknown" : customer.Name,
                Address = (string.IsNullOrWhiteSpace(order.DeliveryAddress) ? order.CustomerAddress : order.DeliveryAddress).Trim(),
                City = (string.IsNullOrWhiteSpace(order.DeliveryCity) ? order.CustomerCity : order.DeliveryCity).Trim(),
                State = (string.IsNullOrWhiteSpace(order.DeliveryRegion) ? order.CustomerRegion : order.DeliveryRegion).Trim(),
                Post_code = string.IsNullOrWhiteSpace(order.DeliveryZip) ? order.CustomerZip : order.DeliveryZip,
                Country = "AU",
                Phone_No = (string.IsNullOrWhiteSpace(order.DeliveryPhone) ? order.CustomerPhone : order.DeliveryPhone).Replace(" ", "").Replace("-", "")
            };

            List<BCTempWebOrder> responseItem = endpointDestinationWriter.PostToBC<BCTempWebOrder>(url, JsonSerializer.Serialize(tempWebOrderHeader), null, true);

            _logger.Info(JsonSerializer.Serialize(responseItem));

            if (responseItem == null)
            {
                if (DebugLog) _logger.Info("Error creating TempWebOrder Header");
                throw new Exception("Error creating TempWebOrder Header");
            }
        }

        public void CreateTempWebOrderLines(Order order)
        {
            //https://api.businesscentral.dynamics.com/v2.0/{{env}}/ODataV4/Company('MMT%20AU')/TempWebOrder

            EndpointService endpointService = new EndpointService();
            Endpoint endpoint = endpointService.GetEndpoints().First(x => x.Name == "TempWebOrder");
            if (endpoint == null) throw new Exception("Endpoint not found: TempWebOrder");
            EndpointHelper endpointDestinationWriter = new EndpointHelper(30, DebugLog ? _logger : null, endpoint.Collection.AuthenticationId);
            string url = endpoint.Url;

            if (DebugLog) _logger.Info("Creating TempWebOrder Lines URL: " + url);
            foreach (var orderLine in order.ProductOrderLines)
            {
                if (DebugLog) _logger.Info("Creating TempWebOrder Line for Product: " + orderLine.ProductNumber);
                BCTempWebOrder tempWebOrderLine = new BCTempWebOrder
                {
                    Type = "Line",
                    Order_No = order.Id,
                    Item_No = orderLine.ProductNumber,
                    Quantity = orderLine.Quantity
                };

                List<BCTempWebOrder> responseItem = endpointDestinationWriter.PostToBC<BCTempWebOrder>(url, JsonSerializer.Serialize(tempWebOrderLine), null, true);

                if (DebugLog) _logger.Info(JsonSerializer.Serialize(responseItem));

                if (responseItem == null)
                {
                    if (DebugLog) _logger.Info("Error creating TempWebOrder Line");
                    throw new Exception("Error creating TempWebOrder Line");
                }
            }
        }

        public List<BCWebOrderFreightQuote> GetFreightQuote(Order order)
        {
            //https://api.businesscentral.dynamics.com/v2.0/{{env}}/ODataV4/Company('MMT%20AU')/WebOrderFreighQuote

            EndpointService endpointService = new EndpointService();
            Endpoint endpoint = endpointService.GetEndpoints().First(x => x.Name == "WebOrderFreightQuote");
            if (endpoint == null) throw new Exception("Endpoint not found: WebOrderFreightQuote");
            EndpointHelper endpointDestinationWriter = new EndpointHelper(30, DebugLog ? _logger : null, endpoint.Collection.AuthenticationId);
            string url = endpoint.Url + "?$filter=Order_No eq '" + order.Id + "'";

            if (DebugLog) _logger.Info("Retreiving Freight Quote: " + url);
            try
            {
                List<BCWebOrderFreightQuote> responseItem = endpointDestinationWriter.GetFromBC<BCWebOrderFreightQuote>(url);
                if (DebugLog) _logger.Info(JsonSerializer.Serialize(responseItem));
                if (responseItem == null || !responseItem.Any())
                {
                    return new List<BCWebOrderFreightQuote>();
                }
                return responseItem;
            }
            catch (Exception ex)
            {
                using var doc = JsonDocument.Parse(ex.Message);
                if (DebugLog) _logger.Error(doc.RootElement.GetProperty("error").GetProperty("message").GetString());
                return new List<BCWebOrderFreightQuote>();
            }
        }
        
        private static void SetCachedResult(string cacheKey, IEnumerable<string> result)
        {
            Caching.Cache.Current.Set(cacheKey, result, new Caching.CacheItemPolicy() { AbsoluteExpiration = DateTimeOffset.Now.AddMinutes(10) });
        }

        private static IList<string> GetCachedResult(string cacheKey)
        {
            return Caching.Cache.Current.Get<IList<string>>(cacheKey);
        }
    }
    
}

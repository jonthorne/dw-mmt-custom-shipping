using Dynamicweb.Updates;
using System.Collections.Generic;

namespace Dynamicweb.MMT.Custom.Shipping.Updates
{
    public class DynamicShipUpdateProvider : UpdateProvider
    {
        public override IEnumerable<Update> GetUpdates()
        {
            var type = GetType();

            return new List<Update>() {
                new FileUpdate("1", this, "/Files/Templates/eCom7/ShippingProvider/DynamicShip.cshtml", () => { 
                    return type.Assembly.GetManifestResourceStream($"{type.Namespace}.DynamicShip.cshtml"); 
                })
            };
        }
    }
}

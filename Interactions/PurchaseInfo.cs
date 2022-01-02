using System;
using System.Runtime.Serialization;

namespace Interactions
{
    [DataContract]
    public class PurchaseInfo
    {
        [DataMember] public string Location { get; set; }
        [DataMember] public decimal Cost { get; set; }
        [DataMember] public DateTimeOffset Time { get; set; }
    }
}
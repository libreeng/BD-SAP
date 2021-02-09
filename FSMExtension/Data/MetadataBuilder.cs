namespace FSMExtension.Data
{
    public interface IMetadataBuilder
    {
        object Build(string metaString);
    }

    public class FsmMetadataBuilder : IMetadataBuilder
    {
        public const string ActivityCode = "act";
        public const string EquipmentCode = "eqp";


        public object Build(string metaString)
        {
            var obj = new FsmMetadataObject();
            if (!string.IsNullOrEmpty(metaString))
            {
                var parts = metaString.Split(';');
                foreach (var part in parts)
                {
                    var subParts = part.Split(':');
                    if (subParts.Length == 2)
                    {
                        switch (subParts[0])
                        {
                            case ActivityCode:
                                obj.ActivityCode = subParts[1];
                                break;

                            case EquipmentCode:
                                obj.EquipmentCode = subParts[1];
                                break;
                        }
                    }
                }
            }

            return obj;
        }

        private class FsmMetadataObject
        {
            public string ActivityCode { get; set; }

            public string EquipmentCode { get; set; }
        }
    }
}

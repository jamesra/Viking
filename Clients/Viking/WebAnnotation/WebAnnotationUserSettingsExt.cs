
namespace rouge1.codepharm.net.XSD.WebAnnotationUserSettings.xsd
{
    using System;
    //using Xml.Schema.Linq;
    using System.Windows.Forms;


    public partial class Hotkey
    {
        public System.Windows.Forms.Keys KeyCode
        {
            get
            {
                KeysConverter conv = new KeysConverter();
                return (Keys)conv.ConvertFrom(KeyName);

            }
        }
    }

    public partial class Parameters
    {
        public int Count => Action.Count +
                       Value.Count +
                       Variable.Count;
    }

    public partial class CreateStructureCommandAction
    {
        public string[] AttributeList
        {
            get
            {
                if (Tags == null)
                {
                    return new string[0];
                }

                return Tags.Split(';');
            }
        }
    }

    public partial class Action
    {
        public void ExecuteAction(out System.Type type, out object[] parameters)
        {
            //Parse the parameters
            parameters = new object[Parameters.Count];
            foreach (Variable variable in Parameters.Variable)
            {
                System.Type targetType = System.Type.GetType(variable.Object);
                System.Reflection.PropertyInfo propInfo = targetType.GetProperty(variable.Property);
                //Doesn't work... not sure how to read the property...
            }

            foreach (Action action in Parameters.Action)
            {
                action.ExecuteAction(out Type targetType, out object[] actionParams);
                parameters[action.Index.Value] = Activator.CreateInstance(targetType, actionParams);
            }

            foreach (Value value in Parameters.Value)
            {
                Type targetType = System.Type.GetType(value.Type);
                parameters[value.Index] = System.Convert.ChangeType(value.Value1, targetType);
            }

            type = System.Type.GetType(Type);
            return;
        }
    }
}
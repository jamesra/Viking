using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NetTopologySuite.Geometries;
using System;
using System.Linq.Expressions;


namespace Viking.DataModel.Annotation.ValueConverters
{
    class CurvePolygonConverter<IN, OUT> : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter
        where OUT: class
        where IN: class

    {
        /*public CurvePolygonConverter([NotNullAttribute] LambdaExpression convertToProviderExpression, 
                                     [NotNullAttribute] LambdaExpression convertFromProviderExpression, 
                                     ConverterMappingHints mappingHints = null) : base(convertToProviderExpression, convertFromProviderExpression, mappingHints)
        {
        }
        */
        public CurvePolygonConverter() : base(ToProvider, FromProvider)
        { }

        static Expression<Func<OUT, IN>> ToProvider = x => DoConvertToProvider(x) as IN;
        static Expression<Func<IN, OUT>> FromProvider = x => DoConvertFromProvider(x) as OUT;

        public override Func<object, object> ConvertToProvider => DoConvertToProvider;

        public override Func<object, object> ConvertFromProvider => DoConvertFromProvider;

        public override Type ModelClrType => typeof(OUT);

        public override Type ProviderClrType => typeof(IN);

        public override ConverterMappingHints MappingHints => base.MappingHints;

        protected static object DoConvertToProvider(object input)
        {
            return input as IN;
        }


        protected static object DoConvertFromProvider(object input)
        { 
            //if (input is IUnsupportedGeometry)
            {
                //App specific logic
                return null; //This is going to be a circle, later we'll need to convert null geometry to circles
            }
            if(input is Geometry shape)
            {
                return shape;
            } 
            throw new NotImplementedException($"Unexpected type {input?.GetType()} passed to converter");
        }
    }
}

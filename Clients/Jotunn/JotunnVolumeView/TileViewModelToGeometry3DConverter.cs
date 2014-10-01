using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D; 
using System.Windows.Data;
using Viking.VolumeViewModel;
using System.Diagnostics;

namespace Viking.VolumeView
{
    class TileViewModelToGeometry3DConverter : IValueConverter
    {
        #region IValueConverter Members

        object IValueConverter.Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            IEnumerable<TileViewModel> tiles = value as IEnumerable<TileViewModel>;
       //     Debug.Assert(tiles != null);

            if (tiles == null)
                return new Model3DCollection();

            if (tiles.Count<TileViewModel>() == 0)
                return new Model3DCollection(); 

            Model3DCollection ModelCollection = new Model3DCollection(tiles.Count<TileViewModel>());
            foreach (TileViewModel t in tiles)
            {
                ModelCollection.Add(t.Model);
            }

            return ModelCollection; 
        }

        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }

    class TileMappingViewModelToModel3DCollection : IValueConverter
    {
        object IValueConverter.Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            TileMappingViewModel tileMappingViewModel = value as TileMappingViewModel;
            //Debug.Assert(tileMappingViewModel != null);

            if (tileMappingViewModel == null)
                return new Model3DCollection();

            if (tileMappingViewModel.Tiles.Count == 0)
                return new Model3DCollection(); 

            Model3DCollection ModelCollection = new Model3DCollection(tileMappingViewModel.Tiles.Count<TileViewModel>());
            foreach (TileViewModel t in tileMappingViewModel.Tiles)
            {
                ModelCollection.Add(t.Model);
            }
            return ModelCollection;
        }

        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    class TileViewModelArrayToModel3DCollection : IValueConverter
    {
        object IValueConverter.Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            IList<TileViewModel> tileViewModels = value as IList<TileViewModel>;
            //Debug.Assert(tileMappingViewModel != null);

            if (tileViewModels == null)
                return new Model3DCollection();

            Model3DCollection ModelCollection = new Model3DCollection(tileViewModels.Count<TileViewModel>());
            foreach (TileViewModel t in tileViewModels)
            {
                ModelCollection.Add(t.Model);
            }
            return ModelCollection;
        }

        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

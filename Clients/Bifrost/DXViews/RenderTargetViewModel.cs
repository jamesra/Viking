using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EmptyKeys.UserInterface.Mvvm;
using EmptyKeys.UserInterface.Media.Imaging;

namespace DXViews
{
    public class RenderTargetViewModel : ViewModelBase
    {
        private BitmapImage renderTargetSource = new BitmapImage();

        private int _Height;

        private int _Width;

        /// <summary>
        /// Gets or sets the render target source.
        /// </summary>
        /// <value>
        /// The render target source.
        /// </value>
        public BitmapImage RenderTargetSource
        {
            get { return renderTargetSource; }
            set { SetProperty(ref renderTargetSource, value); }
        }

        public int Height
        {
            get { return _Height; }
            set { SetProperty(ref _Height, value); }
        }

        public int Width
        {
            get { return _Width; }
            set { SetProperty(ref _Width, value); }
        }
    }
}
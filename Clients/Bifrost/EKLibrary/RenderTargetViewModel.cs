using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EmptyKeys.UserInterface.Mvvm;
using EmptyKeys.UserInterface.Media.Imaging;

namespace EKLibrary
{
    public class RenderTargetViewModel : ViewModelBase
    {
        private BitmapImage renderTargetSource = new BitmapImage();
         
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
          
    }
}

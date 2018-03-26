using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml.Media.Imaging;


using System.IO;
using Xamarin.Forms.Internals;

namespace Xamarin.Forms.Platform.UWP
{

	public class MyGIFDecoder : Xamarin.Forms.Internals.GIFDecoder
	{
		int _frames = 0;

		protected override void AddBitmap(GIFHeader header, GIFBitmap bitmap)
		{
			_frames++;
			System.Diagnostics.Debug.WriteLine("Frame data. Width = {0} Height = {1} Delay = {2}", header.Width, header.Height, bitmap.Delay);
		}

		protected override void FinishedParsing()
		{
			System.Diagnostics.Debug.WriteLine("Frame count {0}", _frames);
		}

		protected override void StartParsing()
		{
			_frames = 0;
		}
	}

	public sealed class FileImageSourceHandler : IImageSourceHandler
	{
		public Task<Windows.UI.Xaml.Media.ImageSource> LoadImageAsync(ImageSource imagesource, CancellationToken cancellationToken = new CancellationToken())
		{
			Windows.UI.Xaml.Media.ImageSource image = null;
			var filesource = imagesource as FileImageSource;	
			
			if (filesource != null)
			{
				string file = filesource.File;
				image = new BitmapImage(new Uri("ms-appx:///" + file));
			}

			var uri = new Uri(("ms-appx:///" + filesource.File));
			var source = Windows.Storage.StorageFile.GetFileFromApplicationUriAsync(uri).GetResults();
			var stream = source.OpenStreamForReadAsync().Result;

			var decoder = new MyGIFDecoder();
			decoder.ParseAsync(stream, false, false).Wait();

			return Task.FromResult(image);
		}
	}
}
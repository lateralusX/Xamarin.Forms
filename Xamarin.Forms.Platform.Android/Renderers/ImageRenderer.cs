using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using AImageView = Android.Widget.ImageView;
using Xamarin.Forms.Internals;

namespace Xamarin.Forms.Platform.Android
{
	internal interface IImageRendererController
	{
		void SkipInvalidate();
	}

	public class ImageRenderer : ViewRenderer<Image, AImageView>
	{
		bool _isDisposed;
		AnimationDrawable _imageAnimation = null;
		readonly MotionEventHelper _motionEventHelper = new MotionEventHelper();

		public ImageRenderer(Context context) : base(context)
		{
			AutoPackage = false;
		}

		[Obsolete("This constructor is obsolete as of version 2.5. Please use ImageRenderer(Context) instead.")]
		public ImageRenderer()
		{
			AutoPackage = false;
		}

		protected override void Dispose(bool disposing)
		{
			if (_isDisposed)
				return;

			if (_imageAnimation != null)
			{
				_imageAnimation.Dispose();
				_imageAnimation = null;
			}

			_isDisposed = true;

			base.Dispose(disposing);
		}

		protected override AImageView CreateNativeControl()
		{
			return new FormsImageView(Context);
		}

		protected override async void OnElementChanged(ElementChangedEventArgs<Image> e)
		{
			base.OnElementChanged(e);

			if (e.OldElement == null)
			{
				var view = CreateNativeControl();
				SetNativeControl(view);
			}

			_motionEventHelper.UpdateElement(e.NewElement);

			await TryUpdateBitmap(e.OldElement);

			UpdateAspect();
		}

		protected override async void OnElementPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			base.OnElementPropertyChanged(sender, e);

			if (e.PropertyName == Image.SourceProperty.PropertyName)
				await TryUpdateBitmap();
			else if (e.PropertyName == Image.AspectProperty.PropertyName)
				UpdateAspect();
			else if (e.PropertyName == Image.IsAnimationPlayingProperty.PropertyName)
				StartStopAnimation();
		}

		void UpdateAspect()
		{
			if (Element == null || Control == null || Control.IsDisposed())
			{
				return;
			}

			AImageView.ScaleType type = Element.Aspect.ToScaleType();
			Control.SetScaleType(type);
		}

		protected virtual async Task TryUpdateBitmap(Image previous = null)
		{
			// By default we'll just catch and log any exceptions thrown by UpdateBitmap so they don't bring down
			// the application; a custom renderer can override this method and handle exceptions from
			// UpdateBitmap differently if it wants to

			try
			{
				await UpdateBitmap(previous);
			}
			catch (Exception ex)
			{
				Log.Warning(nameof(ImageRenderer), "Error loading image: {0}", ex);
			}
			finally
			{
				((IImageController)Element)?.SetIsLoading(false);
			}
		}

		protected async Task UpdateBitmap(Image previous = null)
		{
			if (Element == null || Control == null || Control.IsDisposed())
			{
				return;
			}

			if (!Element.IsSet(Image.AnimationPlayBehaviorProperty) && !Element.IsSet(Image.IsAnimationPlayingProperty))
			{
				await Control.UpdateBitmap(Element, previous);
			}
			else
			{
				var newAnimation = await Control.UpdateAnimation(Element, previous);
				if (newAnimation != null)
				{
					_imageAnimation?.Dispose();
					_imageAnimation = null;

					_imageAnimation = newAnimation;
					if (_imageAnimation != null && (Image.ImagePlayBehavior)Element.GetValue(Image.AnimationPlayBehaviorProperty) == Image.ImagePlayBehavior.OnLoad)
						_imageAnimation.Start();
				}
			}
		}

		public override bool OnTouchEvent(MotionEvent e)
		{
			if (base.OnTouchEvent(e))
				return true;

			return _motionEventHelper.HandleMotionEvent(Parent, e);
		}

		void StartStopAnimation()
		{
			if (_isDisposed || Element == null || Control == null || _imageAnimation == null)
			{
				return;
			}

			if (Element.IsLoading)
				return;

			if (Element.IsAnimationPlaying && !_imageAnimation.IsRunning)
				_imageAnimation.Start();
			else if (!Element.IsAnimationPlaying && _imageAnimation.IsRunning)
				_imageAnimation.Stop();
		}
	}
}
using System;
using System.Collections.Generic;
using System.Text;

using Xamarin.Forms;
using Xamarin.Forms.Internals;
using Xamarin.Forms.CustomAttributes;
using Xamarin.Forms.PlatformConfiguration;

#if UITEST
using Xamarin.UITest;
using NUnit.Framework;
#endif

namespace Xamarin.Forms.Controls.Issues
{
	[Preserve(AllMembers = true)]
	[Issue(IssueTracker.Github, 1704, "[Enhancement] Basic gif animation features", PlatformAffected.UWP)]
	public class Issue1704 : TestContentPage
	{
		Image _animateImageOnLoad;
		Image _animateImageNone;
		Button _startStop;
		bool _start = true;

		protected override void Init()
		{
			var animateImageOnLoadLabel = new Label { Text = "AnimationPlayBehavior = Image.ImagePlayBehavior.OnLoad" };
			_animateImageOnLoad = new Image();
			_animateImageOnLoad.AnimationPlayBehavior = Image.ImagePlayBehavior.OnLoad;
			_animateImageOnLoad.Source = "ie_retro.gif";

			var animateImageNoneLabel = new Label { Text = "AnimationPlayBehavior = Image.ImagePlayBehavior.None" };
			_animateImageNone = new Image();
			_animateImageNone.AnimationPlayBehavior = Image.ImagePlayBehavior.None;
			_animateImageNone.Source = "ie_retro.gif";

			_startStop = new Button { Text = "Start Animation" };
			_startStop.Clicked += (object sender, EventArgs e) => {
				if (_start)
				{
					if (!_animateImageOnLoad.IsAnimationPlaying)
						_animateImageOnLoad.StartAnimation();
					if (!_animateImageNone.IsAnimationPlaying)
						_animateImageNone.StartAnimation();

					_startStop.Text = "Stop Animation";
					_start = false;
				}
				else
				{
					if (_animateImageOnLoad.IsAnimationPlaying)
						_animateImageOnLoad.StopAnimation();
					if (_animateImageNone.IsAnimationPlaying)
						_animateImageNone.StopAnimation();

					_startStop.Text = "Start Animation";
					_start = true;
				}
			};

			Content = new StackLayout {
				Children = {
					animateImageOnLoadLabel,
					_animateImageOnLoad,
					animateImageNoneLabel,
					_animateImageNone,
					_startStop
				}
			};
		}
	}
}

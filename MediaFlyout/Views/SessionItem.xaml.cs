﻿using MediaFlyout.AppInformation;
using MediaFlyout.Utilities;
using MediaFlyout.ViewModels;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Windows.Media.Control;
using static System.WindowsRuntimeSystemExtensions;

namespace MediaFlyout.Views
{
    public partial class SessionItem : UserControl
    {
        private MediaFlyoutWindow flyout;
        private GlobalSystemMediaTransportControlsSession session;
        private SessionItemViewModel model = new SessionItemViewModel();
        private SourceAppInfo sourceInfo;

        public SessionItem(MediaFlyoutWindow flyout, GlobalSystemMediaTransportControlsSession session)
        {
            InitializeComponent();
            DataContext = model;

            this.flyout = flyout;

            this.session = session;
            session.MediaPropertiesChanged += Session_MediaPropertiesChanged;
            session.PlaybackInfoChanged += Session_PlaybackInfoChanged;

            SourceAppInfo_Initialize();
            UpdateInfo();
        }

        public void Invalidate()
        {
            session.MediaPropertiesChanged -= Session_MediaPropertiesChanged;
            session.PlaybackInfoChanged -= Session_PlaybackInfoChanged;
            sourceInfo?.Dispose();

            sourceInfo = null;
            session = null;
            flyout = null;
        }

        #region UI Management

        private void UpdateInfo()
        {
            if (session == null) return;
            UpdateControls();
            UpdateDetails();
        }

        private void UpdateControls()
        {
            if (session == null) return;
            var playback = session.GetPlaybackInfo();
            model.ToggleButton = IsPlaying(playback) ? "\uE769" : "\uE768";

            if (playback == null) return;
            model.IsPreviousEnabled = playback.Controls.IsPreviousEnabled;
            model.IsPlayPauseEnabled = playback.Controls.IsPauseEnabled || playback.Controls.IsPlayEnabled;
            model.IsNextEnabled = playback.Controls.IsNextEnabled;
        }

        private async void UpdateDetails()
        {
            if (session == null) return;
            GlobalSystemMediaTransportControlsSessionMediaProperties properties;
            try
            {
                properties = await session.TryGetMediaPropertiesAsync();
            } catch (FileNotFoundException)
            {
                return;
            }
            if (properties == null) return;
            model.Title = properties.Title;
            model.Artist = properties.Artist;

            UpdateThumbnail(properties);
        }

        private async void UpdateThumbnail(GlobalSystemMediaTransportControlsSessionMediaProperties properties)
        {
            if (properties.Thumbnail != null)
            {
                using (var stream = await properties.Thumbnail.OpenReadAsync())
                {
                    if (stream != null)
                    {
                        using (var nstream = stream.AsStream())
                        {
                            if (ImageUtil.CreateBitmapImageFromStream(nstream, out var bitmap))
                            {
                                model.Thumbnail = bitmap;
                            }
                        }
                    }
                }
            }
            else
            {
                model.Thumbnail = null;
            }
        }

        private bool IsPlaying(GlobalSystemMediaTransportControlsSessionPlaybackInfo playback)
        {
            return playback != null && 
                playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        }

        #endregion

        #region Buttons

        private async void Previous_Click(object sender, RoutedEventArgs e)
        {
            await session.TrySkipPreviousAsync();
        }

        private async void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            var playback = session.GetPlaybackInfo();
            if (playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
            {
                await session.TryPauseAsync();
            }
            else if (playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused)
            {
                await session.TryPlayAsync();
            }
        }

        private async void Next_Click(object sender, RoutedEventArgs e)
        {
            await session.TrySkipNextAsync();
        }

        private void DoubleClick(object sender, RoutedEventArgs e)
        {
            sourceInfo?.Activate();
        }

        private void AppContainer_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                sourceInfo?.Activate();
            }
        }

        #endregion

        #region Session Management

        private void Session_PlaybackInfoChanged(GlobalSystemMediaTransportControlsSession session, PlaybackInfoChangedEventArgs args)
        {
            UpdateControls();
            flyout?.UpdateStatus();
        }

        private void Session_MediaPropertiesChanged(GlobalSystemMediaTransportControlsSession session, MediaPropertiesChangedEventArgs args)
        {
            UpdateInfo();
        }

        #endregion

        #region Source App Info Management

        private void SourceAppInfo_Initialize()
        {
            sourceInfo = SourceAppInfo.FromAppUserModelId(session.SourceAppUserModelId);
            if (sourceInfo != null)
            {
                sourceInfo.InfoFetched += SourceAppInfo_InfoFetched;
                sourceInfo.FetchInfosAsync();

                InfoContainer.MouseDown += DoubleClick;
            }
        }

        private void SourceAppInfo_InfoFetched(object sender, EventArgs e)
        {
            if (sourceInfo == null) return;
            sourceInfo.InfoFetched -= SourceAppInfo_InfoFetched;

            model.AppName = sourceInfo.DisplayName;
            if (ImageUtil.CreateBitmapImageFromStream(sourceInfo.LogoStream, out var bitmap))
            {
                model.AppIcon = bitmap;
            }
        }

        #endregion
    }
}

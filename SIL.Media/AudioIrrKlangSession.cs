﻿// Copyright (c) 2015 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
#if !MONO
using System;
using System.Diagnostics;
using System.IO;
using IrrKlang;

namespace SIL.Media
{
	public class AudioIrrKlangSession : ISimpleAudioSession, ISimpleAudioWithEvents
	{
		private readonly IAudioRecorder _recorder;
		private readonly ISoundEngine _engine = new ISoundEngine();
		private ISound _sound;
		private bool _thinkWeAreRecording;
		private DateTime _startRecordingTime;
		private DateTime _stopRecordingTime;
		private readonly string _path;
		private readonly ISoundStopEventReceiver _irrklangEventProxy;

		/// <summary>
		/// Will be raised when playing is over
		/// </summary>
		public event EventHandler PlaybackStopped;


		public AudioIrrKlangSession(string filePath)
		{
			_engine.AddFileFactory(new SoundFile(filePath));
			_recorder = new IAudioRecorder(_engine);
			_path = filePath;
			_irrklangEventProxy = new ProxyForIrrklangEvents(this);
		}

		public void Test()
		{
			var engine = new ISoundEngine();
			var recorder = new IAudioRecorder(engine);
			var data = recorder.RecordedAudioData; //throws exception.  Should set data to null.
		}

		public string FilePath
		{
			get { return _path; }
		}

		public void StartRecording()
		{
			if (_thinkWeAreRecording)
				throw new ApplicationException("Can't begin recording when we're already recording.");

			_thinkWeAreRecording = true;
			_recorder.ClearRecordedAudioDataBuffer();

			_recorder.StartRecordingBufferedAudio(22000, SampleFormat.Signed16Bit, 1);
			//_recorder.StartRecordingBufferedAudio();
			_startRecordingTime = DateTime.Now;

		}

		public void StopRecordingAndSaveAsWav()
		{
			if (!_thinkWeAreRecording)
				throw new ApplicationException("Stop Recording called when we weren't recording.  Use IsRecording to check first.");

			_thinkWeAreRecording = false;
			_recorder.StopRecordingAudio();
			if (_recorder.RecordedAudioData != null)
			{
				SaveAsWav(FilePath);
			}
			_recorder.ClearRecordedAudioDataBuffer();
			_stopRecordingTime = DateTime.Now;
		}

		public double LastRecordingMilliseconds
		{
			get
			{
				if (_startRecordingTime == default(DateTime) || _stopRecordingTime == default(DateTime))
					return 0;
				return _stopRecordingTime.Subtract(_startRecordingTime).TotalMilliseconds;
			}
		}

		public bool IsRecording
		{
			get { return _recorder != null && _recorder.IsRecording; }
		}

		public bool IsPlaying
		{
			get { return _sound != null && !_sound.Finished; }
		}

		public bool CanRecord
		{
			get { return !IsPlaying && !IsRecording; }
		}

		public bool CanStop
		{
			get { return IsPlaying || IsRecording; }
		}

		public bool CanPlay
		{
			get { return !IsPlaying && !IsRecording && File.Exists(_path); }
		}

		public void Play()
		{
			if (IsRecording)
				throw new ApplicationException("Can't play while recording.");

			if (_sound != null)
			{
				_engine.RemoveAllSoundSources();
				_engine.StopAllSounds();
				Dispose();
			}

			if (!File.Exists(_path))
				throw new FileNotFoundException("Could not find sound file", _path);

			_sound = _engine.Play2D(_path);
			if (_sound != null)
			{
				_sound.setSoundStopEventReceiver(_irrklangEventProxy, _engine);
				return;
			}
			if (new FileInfo(_path).Length == 0) throw new Exception("Empty File");
			// if BytesPerSecond is 0 or _sound is null, it's probably a format we don't recognize. See if the OS knows how to play it.
			Process.Start(_path);
		}

		private class SoundFile : IFileFactory
		{
			private readonly string _fileSoundPath;

			public SoundFile(string filePath)
			{
				_fileSoundPath= filePath;
			}

			// myFile doesn't retain unicode characters so we capture it when the object is created
			public Stream openFile(string myFile)
			{
				// Ensure that the file is not locked from other users
				return File.Open(_fileSoundPath, FileMode.Open, FileAccess.Read);
			}
		}

		/// <summary>
		/// This is better than putting the ISoundStopEventReceiver on our class, because doing *that*
		/// requires clients then to reference the irrklang assembly, where it is defined.
		/// And this is just a private mater, since we expose the event through a normal .net event handler
		/// </summary>
		private class ProxyForIrrklangEvents : ISoundStopEventReceiver
		{
			private readonly AudioIrrKlangSession _session;

			public ProxyForIrrklangEvents(AudioIrrKlangSession session)
			{
				_session = session;
			}

			public void OnSoundStopped(ISound sound, StopEventCause reason, object userData)
			{
				_session.OnSoundStopped(sound, reason, userData);
			}
		}

		private void OnSoundStopped(ISound sound, StopEventCause reason, object userData)
		{
			Dispose();
		}

		public void SaveAsWav(string path)
		{

			if (File.Exists(path))
				File.Delete(path);

			short formatType = 1;
			var numChannels = _recorder.AudioFormat.ChannelCount;
			var sampleRate = _recorder.AudioFormat.SampleRate;
			var bitsPerChannel = _recorder.AudioFormat.SampleSize*8;
			var bytesPerSample = _recorder.AudioFormat.FrameSize;
			var bytesPerSecond = _recorder.AudioFormat.BytesPerSecond;
			var dataLen = _recorder.AudioFormat.SampleDataSize;

			const int fmtChunkLen = 16;
			const int waveHeaderLen = 4 + 8 + fmtChunkLen + 8;

			var totalLen = waveHeaderLen + dataLen;

			using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
			{

				using (BinaryWriter bw = new BinaryWriter(fs))
				{
					bw.Write(new char[4] {'R', 'I', 'F', 'F'});

					bw.Write(totalLen);

					bw.Write(new [] {'W', 'A', 'V', 'E', 'f', 'm', 't', ' '});

					bw.Write((int) fmtChunkLen);

					bw.Write((short) formatType);
					bw.Write((short) numChannels);

					bw.Write((int) sampleRate);

					bw.Write((int) bytesPerSecond);

					bw.Write((short) bytesPerSample);

					bw.Write((short) bitsPerChannel);

					bw.Write(new [] {'d', 'a', 't', 'a'});
					bw.Write(_recorder.RecordedAudioData.Length);

					bw.Write(_recorder.RecordedAudioData);
					bw.Close();
				}
				fs.Close();
			}

			_recorder.ClearRecordedAudioDataBuffer();
		}

		public void StopPlaying()
		{
			if (_sound != null && !_sound.Finished)
				_sound.Stop();
			try
			{
				_engine.RemoveAllSoundSources();
			}
			catch (Exception)
			{
				// We'll just ignore any errors on stopping the sounds (they probably aren't playing).
			}
			_engine.StopAllSounds();
			Dispose();
		}

		public void Dispose()
		{
			_recorder.Dispose();
			if (_sound != null)
				_sound.Dispose();
			_sound = null;
		}
	}
}

/*
 * from forum:
 *
 * I'm currently making a childs game where I need to play pianosounds(multi voice). Thought I'd use IrrKlang. Worked just fine for a start. Then I discovered that the audio stopped working after running the app for about 40-50 seconds in idle mode.
 *
 * Solved it!

I use an option on the constructor of the soundEngine(SoundOutputDriver.WinMM);

And I set "nostreaming" and "preload" true - now it works like a charm!
 */
#endif

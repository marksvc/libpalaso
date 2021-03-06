﻿// Copyright (c) 2013 SIL International
// This software is licensed under the MIT License (http://opensource.org/licenses/MIT)
#if __MonoCS__
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using X11.XKlavier;
using Palaso.UI.WindowsForms.Keyboarding.Interfaces;
using Palaso.UI.WindowsForms.Keyboarding.InternalInterfaces;
using Palaso.WritingSystems;
using Palaso.Reporting;

namespace Palaso.UI.WindowsForms.Keyboarding.Linux
{
	/// <summary>
	/// This class handles initializing the list of keyboards on Ubuntu versions >= 13.10.
	/// Previously there were two separate keyboard icons for XKB and for IBus keyboards.
	/// Starting with 13.10 there is now just one and the list of keyboards gets stored in
	/// a different dconf key (/org/gnome/desktop/input-sources/sources). This adaptor reads
	/// the list of keyboards and then delegates the individual keyboards to the respective
	/// real keyboard controller.
	/// XKB keyboards get handled by IBus as well.
	/// </summary>
	public class CombinedKeyboardAdaptor: IKeyboardAdaptor
	{
		private static bool HasCombinedKeyboards = true;
		private Dictionary<string,int> IbusKeyboards;
		private Dictionary<string,int> XkbKeyboards;
		private int _kbdIndex;
		private IntPtr _client = IntPtr.Zero;

		#region P/Invoke imports for glib and dconf
		// NOTE: we directly use glib/dconf methods here since we don't want to
		// introduce an otherwise unnecessary dependency on gconf-sharp/gnome-sharp.
		[DllImport("libgobject-2.0-0.dll")]
		private extern static void g_type_init();

		[DllImport("libgobject-2.0-0.dll")]
		private extern static string g_variant_print(IntPtr variant, bool typeAnnotate);

		[DllImport("libgobject-2.0-0.dll")]
		private extern static IntPtr g_variant_new_uint32(UInt32 value);

		[DllImport("libgobject-2.0-0.dll")]
		private extern static void g_object_unref(IntPtr obj);

		[DllImport("libdconf.dll")]
		private extern static IntPtr dconf_client_new();

		[DllImport("libdconf.dll")]
		private extern static IntPtr dconf_client_read(IntPtr client, string key);

		[DllImport("libdconf.dll")]
		private extern static bool dconf_client_write_sync(IntPtr client, string key, IntPtr value,
			ref string tag, IntPtr cancellable, out IntPtr error);
		#endregion

		public CombinedKeyboardAdaptor()
		{
			g_type_init();
		}

		private static string GetValue(string raw)
		{
			return raw.Trim().Trim('\'');
		}

		private void RegisterXkbKeyboards()
		{
			if (XkbKeyboards.Count <= 0)
				return;

			var xkbAdaptor = GetAdaptor<XkbKeyboardAdaptor>();
			var configRegistry = XklConfigRegistry.Create(xkbAdaptor.XklEngine);
			var layouts = configRegistry.Layouts;
			foreach (var kvp in layouts)
			{
				foreach (var layout in kvp.Value)
				{
					int index;
					if ((XkbKeyboards.TryGetValue(layout.LayoutId, out index) && layout.LayoutId == layout.LanguageCode) ||
						XkbKeyboards.TryGetValue(string.Format("{0}+{1}", layout.LanguageCode, layout.LayoutId), out index))
					{
						xkbAdaptor.AddKeyboardForLayout(layout, index, this);
					}
				}
			}
		}

		private void RegisterIbusKeyboards()
		{
			if (IbusKeyboards.Count <= 0)
				return;

			var ibusAdaptor = GetAdaptor<IbusKeyboardAdaptor>();
			List<string> missingLayouts = new List<string>(IbusKeyboards.Keys);
			foreach (var ibusKeyboard in ibusAdaptor.GetAllIBusKeyboards())
			{
				if (IbusKeyboards.ContainsKey(ibusKeyboard.LongName))
				{
					missingLayouts.Remove(ibusKeyboard.LongName);
					var keyboard = new IbusKeyboardDescription(this, ibusKeyboard);
					keyboard.SystemIndex = IbusKeyboards[ibusKeyboard.LongName];
					KeyboardController.Manager.RegisterKeyboard(keyboard);
				}
			}
			foreach (var layout in missingLayouts)
				Console.WriteLine("Didn't find " + layout);
		}

		private static T GetAdaptor<T>() where T: class
		{
			foreach (var adaptor in KeyboardController.Adaptors)
			{
				var tAdaptor = adaptor as T;
				if (tAdaptor != default(T))
					return tAdaptor;
			}
			return default(T);
		}

		private void AddKeyboard(string source)
		{
			var parts = source.Trim('(', ')').Split(',');
			Debug.Assert(parts.Length == 2);
			if (parts.Length != 2)
				return;

			var type = GetValue(parts[0]);
			var layout = GetValue(parts[1]);
			if (type == "xkb")
				XkbKeyboards.Add(layout, _kbdIndex);
			else
				IbusKeyboards.Add(layout, _kbdIndex);
			++_kbdIndex;
		}

		private void AddKeyboards(string source)
		{
			int startNextSegment = source.IndexOf("), (", StringComparison.InvariantCulture);
			if (startNextSegment < 0)
				AddKeyboard(source);
			else
			{
				AddKeyboard(source.Substring(0, startNextSegment));
				AddKeyboards(source.Substring(startNextSegment + 4));
			}
		}

		private void AddAllKeyboards(string source)
		{
			_kbdIndex = 0;
			AddKeyboards(source);
			RegisterIbusKeyboards();
			RegisterXkbKeyboards();
		}

		#region IKeyboardAdaptor implementation
		public void Initialize()
		{
			if (!HasCombinedKeyboards)
				return;

			IbusKeyboards = new Dictionary<string, int>();
			XkbKeyboards = new Dictionary<string, int>();

			try
			{
				KeyboardController.CombinedKeyboardHandling = false;
				_client = dconf_client_new();
			}
			catch (DllNotFoundException)
			{
				_client = IntPtr.Zero;
				// Older Ubuntu versions have a version of the dconf library with a
				// different version number (different from what libdconf.dll gets
				// mapped to in app.config). However, since those Ubuntu versions
				// don't have combined keyboards this really doesn't matter.
				HasCombinedKeyboards = false;
				return;
			}

			// This is the proper path for the combined keyboard handling, not the path
			// given in the IBus reference documentation.
			var sources = dconf_client_read(_client, "/org/gnome/desktop/input-sources/sources");
			if (sources == IntPtr.Zero)
			{
				HasCombinedKeyboards = false;
				return;
			}
			KeyboardController.CombinedKeyboardHandling = true;

			AddAllKeyboards(g_variant_print(sources, false).Trim('[', ']'));
		}

		public void UpdateAvailableKeyboards()
		{
			Initialize();
		}

		public void Close()
		{
			if (_client != IntPtr.Zero)
			{
				g_object_unref(_client);
				_client = IntPtr.Zero;
			}
		}

		public bool ActivateKeyboard(IKeyboardDefinition keyboard)
		{
			Debug.Assert(keyboard is KeyboardDescription);
			Debug.Assert(((KeyboardDescription)keyboard).Engine == this);
			if (keyboard is XkbKeyboardDescription)
			{
				var xkbKeyboard = keyboard as XkbKeyboardDescription;
				if (xkbKeyboard.GroupIndex >= 0)
					SelectKeyboard(xkbKeyboard.GroupIndex);
			}
			else if (keyboard is IbusKeyboardDescription)
			{
				var ibusKeyboard = keyboard as IbusKeyboardDescription;
				try
				{
					var ibusAdaptor = GetAdaptor<IbusKeyboardAdaptor>();
					if (!ibusAdaptor.CanSetIbusKeyboard())
						return false;
					if (ibusAdaptor.IBusKeyboardAlreadySet(ibusKeyboard))
						return true;
					SelectKeyboard(ibusKeyboard.SystemIndex);
					GlobalCachedInputContext.Keyboard = ibusKeyboard;
				}
				catch (Exception e)
				{
					Debug.WriteLine(string.Format("Changing keyboard failed, is kfml/ibus running? {0}", e));
					return false;
				}
			}
			else
			{
				throw new ArgumentException();
			}
			return true;
		}

		public void DeactivateKeyboard(IKeyboardDefinition keyboard)
		{
			if (keyboard is IbusKeyboardDescription)
			{
				var ibusAdaptor = GetAdaptor<IbusKeyboardAdaptor>();
				if (ibusAdaptor.CanSetIbusKeyboard())
					GlobalCachedInputContext.InputContext.Reset();
			}
			GlobalCachedInputContext.Keyboard = null;
		}

		public IKeyboardDefinition GetKeyboardForInputLanguage(IInputLanguage inputLanguage)
		{
			throw new NotImplementedException();
		}

		public IKeyboardDefinition CreateKeyboardDefinition(string layout, string locale)
		{
			throw new NotImplementedException();
		}

		public List<IKeyboardErrorDescription> ErrorKeyboards
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public IKeyboardDefinition DefaultKeyboard
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public KeyboardType Type
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		#endregion

		internal void SelectKeyboard(int index)
		{
			if (!HasCombinedKeyboards || _client == IntPtr.Zero)
				return;
			IntPtr value = g_variant_new_uint32((uint)index);
			string tag = null;
			IntPtr cancellable = IntPtr.Zero;
			IntPtr error = IntPtr.Zero;
			bool okay = dconf_client_write_sync(_client, "/org/gnome/desktop/input-sources/current", value,
				ref tag, cancellable, out error);
			// Do not call g_variant_unref(value) here.  The documentation says that g_variant_new_uint32
			// returns "a floating reference to a new uint32 GVariant instance.", i.e.
			// "Don't free data after the code is done."
			if (!okay)
			{
				Console.WriteLine("CombinedKeyboardAdaptor.SelectKeyboard({0}) failed", index);
				Logger.WriteEvent("CombinedKeyboardAdaptor.SelectKeyboard({0}) failed", index);
			}
		}
	}
}
#endif

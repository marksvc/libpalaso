﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Palaso.UI.WindowsForms.WritingSystems.WSTree
{
	public partial class GetDialectNameDialog : Form
	{
		public GetDialectNameDialog()
		{
			InitializeComponent();
		}

		public string DialectName {
			get
				{
					return _dialectName.Text;
				}
		}

		private void _okButton_Click(object sender, EventArgs e)
		{
			DialogResult = System.Windows.Forms.DialogResult.OK;
			Close();
		}
	}
}
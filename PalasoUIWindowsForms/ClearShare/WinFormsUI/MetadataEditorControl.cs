﻿using System;
using System.Windows.Forms;

namespace Palaso.UI.WindowsForms.ClearShare.WinFormsUI
{
	public partial class MetadataEditorControl : UserControl
	{
		private Metadata _metadata;
		private bool _settingUp;

		public MetadataEditorControl()
		{
			InitializeComponent();

			_settingUp = true;
			//set some defaults in case they turn on CC
			_shareAlike.Checked = true;
			_nonCommercial.Checked = true;
		}

		public Metadata Metadata
		{
			get { return _metadata; }
			set
			{
				_metadata = value;

				if (_metadata == null)
				{
					this.Visible = false;
					return;
				}
				_settingUp = true;
				this.Visible = true;
				_illustrator.Text = _metadata.Creator;
				_copyrightYear.Text = _metadata.GetCopyrightYear();
				if(_copyrightYear.Text =="")
					_copyrightYear.Text = DateTime.Now.Year.ToString();

				_copyrightBy.Text = _metadata.GetCopyrightBy();
				if(_metadata.License!=null)
					_licenseImage.Image = _metadata.License.GetImage();
				if (_metadata.License is CreativeCommonsLicense)
				{
					var cc = (CreativeCommonsLicense) _metadata.License;
					_creativeCommons.Checked = true;
					_noDerivates.Checked = cc.DerivativeRule == CreativeCommonsLicense.DerivativeRules.NoDerivatives;
					_shareAlike.Checked = cc.DerivativeRule ==
										  CreativeCommonsLicense.DerivativeRules.DerivativesWithShareAndShareAlike;
					_derivatives.Checked = cc.DerivativeRule == CreativeCommonsLicense.DerivativeRules.Derivatives;
					_commercial.Checked = cc.CommercialUseAllowed;
					_nonCommercial.Checked = !cc.CommercialUseAllowed;
					_customRightsStatement.Text = _metadata.License.RightsStatement;
				}
				else if(_metadata.License is CustomLicense)
				{
					_customLicense.Checked = true;
					_customRightsStatement.Text = _metadata.License.RightsStatement;
				}
				else
				{
					_unknownLicense.Checked = true;
				}
				_settingUp = false;
				UpdateDisplay();
			}
		}

		/// <summary>
		/// Set this to false if you don't want to collect info on who created it (e.g. you're just getting copyright/license)
		/// </summary>
		public bool ShowCreator
		{
			get { return _illustrator.Visible; }
			set { _illustrator.Visible = _illustratorLabel.Visible = value; }
		}

		private void OnLicenseComponentChanged(object sender, System.EventArgs e)
		{
			if(_settingUp)
				return;

		    var previousLicense = _metadata.License;

			if (_metadata.License == null || !(_metadata.License is CreativeCommonsLicense))//todo: that's kinda heavy-handed
				_metadata.License = new CreativeCommonsLicense(true, true, CreativeCommonsLicense.DerivativeRules.Derivatives);

			if (_creativeCommons.Checked)
			{
				var cc = (CreativeCommonsLicense) _metadata.License;
				cc.AttributionRequired = true; // for now, we don't have a way to turn that off
				cc.CommercialUseAllowed = _commercial.Checked;
				if (_derivatives.Checked)
					cc.DerivativeRule = CreativeCommonsLicense.DerivativeRules.Derivatives;
				else if (_shareAlike.Checked)
					cc.DerivativeRule = CreativeCommonsLicense.DerivativeRules.DerivativesWithShareAndShareAlike;
				else
					cc.DerivativeRule = CreativeCommonsLicense.DerivativeRules.NoDerivatives;
				_licenseImage.Image = cc.GetImage();

			    // If we're going from custom to CC, we could as easily just copy the statement into CC license.
                // Maybe they want that. If they don't, mabye they'll fail to notice that we turned their 
                // custom license into a CC restriction, or be confused by why we did that.
                // In addition, custom restrictions are so... undesirable / unenforcable. So we have to guess,
                //and we're going to guess on the side of getting rid of it.
			    if (!(previousLicense is CreativeCommonsLicense))
			    {
			        _customRightsStatement.Text = "";
			    }
			}
			if(_unknownLicense.Checked)
			{
				_metadata.License = new NullLicense();
			}
			if (_customLicense.Checked)
			{
			    _metadata.License = new CustomLicense() {RightsStatement = _customRightsStatement.Text};

			}

			UpdateDisplay();
		}

		private void UpdateDisplay()
		{
			panel1.Enabled = panel2.Enabled = _creativeCommons.Checked;
			_licenseImage.Visible = _creativeCommons.Checked;
			_customRightsStatement.Enabled = _customLicense.Checked || _creativeCommons.Checked;
			_linkToRefinedCreativeCommonsWarning.Visible = _creativeCommons.Checked && !string.IsNullOrWhiteSpace(_customRightsStatement.Text) ;
			_additionalRequestsLabel.Visible = _creativeCommons.Checked;
			if (_creativeCommons.Checked)
			{
				_customRightsStatement.Top = _additionalRequestsLabel.Bottom+10;
				_customRightsStatement.Left = _creativeCommons.Left;
			    _customRightsStatement.Width = tableLayoutPanel1.Right - _customRightsStatement.Left; 
			}
			else {
				_customRightsStatement.Top = _additionalRequestsLabel.Top;
				_customRightsStatement.Left = _licenseImage.Left;
                _customRightsStatement.Width = tableLayoutPanel1.Right - _customRightsStatement.Left; 
			}
		}

//        public bool IsMinimallyComplete
//        {
//            get
//            {
//                return _
//            }
//        }

		private void _illustrator_TextChanged(object sender, EventArgs e)
		{
			_metadata.Creator = _illustrator.Text;
		}


		private void _copyrightYear_TextChanged(object sender, EventArgs e)
		{
			if (_settingUp)
				return;
			_metadata.SetCopyrightNotice(_copyrightYear.Text, _copyrightBy.Text);
		}

		private void _customLicenseDescription_TextChanged(object sender, EventArgs e)
		{
			if (_customLicense.Checked)
			{
				var customLicense = _metadata.License as CustomLicense;

				if (customLicense != null)
					customLicense.RightsStatement = _customRightsStatement.Text;
			}
			if (_creativeCommons.Checked)
			{
				var l = _metadata.License as CreativeCommonsLicense;

				l.RightsStatement = _customRightsStatement.Text;
			}
			UpdateDisplay();
		}

		private void _copyrightBy_TextChanged(object sender, EventArgs e)
		{
			if (_settingUp)
				return;
			_metadata.SetCopyrightNotice(_copyrightYear.Text, _copyrightBy.Text);
		}

		private void label2_Click(object sender, EventArgs e)
		{

		}


		/*       private PalasoImage _image;

		public ImageMetadataControl()
		{
			InitializeComponent();
			UpdateDisplay();
		}

		public void SetImage(PalasoImage image)
		{
			SaveChanges();
			_image = image;
			UpdateDisplay();
		}

		private void SaveChanges()
		{
			if (_image != null && _image.Metadata.HasChanges)
			{
				_image.Metadata.Write();
			}
		}


		public override bool ValidateChildren()
		{
			SaveChanges();
			return base.ValidateChildren();
		}
		private void UpdateDisplay()
		{
			if (_image == null)
			{
				_illustrator.ReadOnly = _copyright.ReadOnly == false;
				return;
			}

			this.Visible = _image.Image != null;

			//_lockedCheckbox.Checked = _image.MetadataLocked;
			_illustrator.Text = _image.Metadata.Creator;
			_copyright.Text = _image.Metadata.CopyrightNotice;
			_illustrator.ReadOnly = _copyright.ReadOnly = !_image.Metadata.AllowEditingMetadata;
			_illustrator.BorderStyle = _copyright.BorderStyle = _image.Metadata.AllowEditingMetadata ? BorderStyle.FixedSingle : BorderStyle.None;

			if (_image.Metadata.AllowEditingMetadata)
			{
			}
			//only handle the first one, for now
			if (FirstLicense == null)
			{
				_licenseImage.Image = null;
				//_licenseDescription.Visible = false;
			}
			else
			{
				//                _licenseDescription.ReadOnly = FirstLicense.EditingAllowed;
				//                _licenseDescription.Visible = true;
				//                _licenseDescription.Text = FirstLicense.GetDescription("en");
				_licenseImage.Image = FirstLicense.GetImage();
			}
		}
		private LicenseInfo FirstLicense
		{
			get
			{
				return _image.Metadata.License;
			}
		}



		private void _lockedCheckbox_CheckedChanged(object sender, EventArgs e)
		{
			// _image.MetadataLocked = _lockedCheckbox.Checked;
			UpdateDisplay();
		}

		private void _licenseDescription_TextChanged(object sender, EventArgs e)
		{
			// FirstLicense.SetDescription("en",_licenseDescription.Text);
		}

		private void ImageMetadataControl_Validating(object sender, CancelEventArgs e)
		{
			SaveChanges();
		}

		private void ImageMetadataControl_Load(object sender, EventArgs e)
		{
			ParentForm.FormClosing += new FormClosingEventHandler((s, o) => SaveChanges());
		}

		//TODO: need a simple chooser (combo box?) for Creative Commons or custom, if custom, show editable description.
  */

	}
}

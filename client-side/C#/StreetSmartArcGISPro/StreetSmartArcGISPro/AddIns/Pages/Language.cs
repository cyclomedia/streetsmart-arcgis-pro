/*
 * Street Smart integration in ArcGIS Pro
 * Copyright (c) 2018 - 2019, CycloMedia, All rights reserved.
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3.0 of the License, or (at your option) any later version.
 * 
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 * 
 * You should have received a copy of the GNU Lesser General Public
 * License along with this library.
 */

using System.ComponentModel;

using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using ArcGIS.Desktop.Framework.Contracts;

using StreetSmartArcGISPro.Configuration.File;
using StreetSmartArcGISPro.Configuration.Resource;
using StreetSmartArcGISPro.Utilities;

using SelectedLanguage = StreetSmartArcGISPro.Configuration.Resource.Language;

namespace StreetSmartArcGISPro.AddIns.Pages
{
  internal class Language: Page, INotifyPropertyChanged
  {
    #region Events

    public new event PropertyChangedEventHandler PropertyChanged;

    #endregion

    #region Members

    private readonly LanguageSettings _languageSettings;
    private readonly SelectedLanguage _selectedLanguage;

    #endregion

    #region Constructors

    protected Language()
    {
      Languages = Languages.Instance;
      _languageSettings = LanguageSettings.Instance;
      _selectedLanguage = _languageSettings.Language;
    }

    #endregion

    #region Properties

    /// <summary>
    /// All Languages
    /// </summary>
    public Languages Languages { get; }

    /// <summary>
    /// Selected Language
    /// </summary>
    public SelectedLanguage SelectedLanguage
    {
      get => _languageSettings.Language;
      set
      {
        if (_languageSettings.Language != value)
        {
          IsModified = true;
          _languageSettings.Language = value;
          LocalizationProvider.UpdateAllObjects();
          NotifyPropertyChanged();
        }
      }
    }

    #endregion

    #region Overrides

    protected override Task CommitAsync()
    {
      _languageSettings.Save();
      return base.CommitAsync();
    }

    protected override Task CancelAsync()
    {
      _languageSettings.Language = _selectedLanguage;
      _languageSettings.Save();
      return base.CancelAsync();
    }

    #endregion

    #region Functions

    protected override void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
  }
}

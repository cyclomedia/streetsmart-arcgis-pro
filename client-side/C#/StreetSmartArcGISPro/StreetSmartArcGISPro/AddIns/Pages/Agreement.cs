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

using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

using FileAgreement = StreetSmartArcGISPro.Configuration.File.Agreement;

namespace StreetSmartArcGISPro.AddIns.Pages
{
  internal class Agreement : Page
  {
    #region Events

    public new event PropertyChangedEventHandler PropertyChanged;

    #endregion

    #region Members

    private readonly FileAgreement _agreement;

    private bool _value;

    #endregion

    #region Constructors

    protected Agreement()
    {
      _agreement = FileAgreement.Instance;
      _value = _agreement.Value;
    }

    #endregion

    #region Properties

    public bool Value
    {
      get => _value;
      set
      {
        if (_value != value)
        {
          IsModified = true;
          _value = value;
          NotifyPropertyChanged();
        }
      }
    }

    public string AgreementText
    {
      get
      {
        Type type = GetType();
        Assembly assembly = type.Assembly;
        const string agreementPath = "StreetSmartArcGISPro.Resources.Agreement.txt";
        Stream agreementStream = assembly.GetManifestResourceStream(agreementPath);
        string result = string.Empty;

        if (agreementStream != null)
        {
          var reader = new StreamReader(agreementStream);
          result = reader.ReadToEnd();
          reader.Close();
        }

        return result;
      }
    }

    #endregion

    #region Overrides

    protected override Task CommitAsync()
    {
      _agreement.Value = _value;
      _agreement.Save();

      if (_value)
      {
        FrameworkApplication.State.Activate("streetSmartArcGISPro_agreementAcceptedState");
      }

      return base.CommitAsync();
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

﻿/*
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

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

using ArcGIS.Desktop.Mapping;

using StreetSmartArcGISPro.Utilities;

using SystemIOFile = System.IO.File;
using FileProject = StreetSmartArcGISPro.Configuration.File.Project;
using ArcGisProject = ArcGIS.Desktop.Core.Project;

namespace StreetSmartArcGISPro.Configuration.File
{
  [XmlRoot("Projects")]
  public class ProjectList: ObservableCollection<Project>
  {
    #region Events

    protected override event PropertyChangedEventHandler PropertyChanged;

    #endregion

    #region Members

    private static readonly XmlSerializer XmlProjects;
    private static ProjectList _projectList;

    #endregion

    #region Constructors

    static ProjectList()
    {
      XmlProjects = new XmlSerializer(typeof(ProjectList));
    }

    #endregion

    #region Properties

    public static ProjectList Instance
    {
      get
      {
        if (_projectList == null)
        {
          Load();
        }

        return _projectList ?? (_projectList = Create());
      }
    }

    private static string FileName => Path.Combine(FileUtils.FileDir, "Projects.xml");

    #endregion

    #region Functions

    public void Save()
    {
      FileStream streamFile = SystemIOFile.Open(FileName, FileMode.Create);
      XmlProjects.Serialize(streamFile, this);
      streamFile.Close();
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static void Load()
    {
      if (SystemIOFile.Exists(FileName))
      {
        var streamFile = new FileStream(FileName, FileMode.OpenOrCreate);
        _projectList = (ProjectList) XmlProjects.Deserialize(streamFile);
        streamFile.Close();
      }
    }

    private static ProjectList Create()
    {
      var result = new ProjectList();
      result.Save();
      return result;
    }

    public Setting GetSettings(string projectUri, string mapUri)
    {
      var project =
        this.Aggregate<FileProject, FileProject>(null,
          (current, element) => element.Uri == projectUri ? element : current);

      if (project == null && !string.IsNullOrEmpty(projectUri))
      {
        project = FileProject.Create(projectUri);
        Add(project);
        Save();
      }

      var settings = project?.GetSettings(mapUri);

      if (project != null && settings == null && !string.IsNullOrEmpty(mapUri))
      {
        settings = Setting.Create(mapUri);
        project.Settings.Add(settings);
        Save();
      }

      return settings;
    }

    public Setting GetSettings(MapView mapView)
    {
      string projectUri = ArcGisProject.Current?.URI;
      string mapUri = mapView?.Map?.URI;
      return GetSettings(projectUri, mapUri);
    }

    #endregion
  }
}

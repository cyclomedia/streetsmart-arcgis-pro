/*
 * Street Smart .NET integration
 * Copyright (c) 2016 - 2021, CycloMedia, All rights reserved.
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

namespace StreetSmartArcGISPro.SLD
{
  /// <summary>
  /// Styled layer descriptor
  /// </summary>
  public interface IStyledLayerDescriptor
  {
    /// <summary>
    /// Version number
    /// </summary>
    string Version { get; set; }

    /// <summary>
    /// the user layer
    /// </summary>
    UserLayer UserLayer { get; set; }

    /// <summary>
    /// The Serialization of SLD
    /// </summary>
    // ReSharper disable once InconsistentNaming
    string GetSerializedSld();
  }
}

﻿using System.Drawing.Imaging;
using Editor.Controls;
using Editor.DataClasses.GameDataClasses;
using Editor.DataClasses.MapModes;
using Editor.DataClasses.Saveables;
using Editor.DataClasses.Settings;
using Editor.Helper;
using Editor.Properties;

namespace Editor.Forms.Feature
{
   public partial class SelectionDrawerForm : Form
   {
      private ZoomControl ZoomControl { get; set; } = new(new(Globals.MapWidth, Globals.MapHeight, PixelFormat.Format32bppArgb));
      private List<DrawingLayer> layers = [];

      public SelectionDrawerForm()
      {
         InitializeComponent();

         MainLayoutPanel.Controls.Add(ZoomControl, 1, 0);
         ZoomControl.FocusOn(new Rectangle(0, 0, Globals.MapWidth, Globals.MapHeight));
         
         MapModeManager.MapModeChanged += OnMapModeChanged;
         Selection.OnProvinceGroupDeselected += RenderOnEvent;
         Selection.OnProvinceGroupSelected += RenderOnEvent;

         if (Globals.Settings.Gui.SelectionDrawerAlwaysOnTop)
            TopMost = true;

         FormClosing += OnFormClose;

         LayerListView.FullRowSelect = true;

         LayerListView.ItemMoved += ListBoxOnItemMoved;

         LayerListView.KeyDown += (s, e) =>
         {
            if (e.KeyCode == Keys.Delete)
            {
               if (LayerListView.SelectedIndices.Count == 1)
               {
                  layers.RemoveAt(LayerListView.SelectedIndices[0]);
                  LayerListView.Items.RemoveAt(LayerListView.SelectedIndices[0]);
                  RenderImage();
               }
            }
         };

         LayerListView.MouseDoubleClick += (s, e) =>
         {
            if (LayerListView.SelectedItems.Count == 1)
            {
               var layer = layers[LayerListView.SelectedIndices[0]];
               var popup = new PopUpForm(layer)
               {
                  StartPosition = FormStartPosition.CenterParent,
                  TopMost = true
               };

               // Set the owner to the always-on-top form
               popup.PropertyChanged += (s, e) => RenderImage();
               popup.ShowDialog(this);
               RenderImage();

            }
         };

         OptionComboBox.Items.AddRange([.. Enum.GetNames(typeof(DrawingOptions))]);
         ImageSizeBox.Items.AddRange([.. Enum.GetNames(typeof(ImageSize))]);
         ImageSizeBox.SelectedIndex = 0;

      }

      private void ListBoxOnItemMoved(object? sender, SwappEventArgs e)
      {
         var layer = layers[e.From];
         layers.RemoveAt(e.From);
         layers.Insert(e.To, layer);
         RenderImage();
      }

      private void OnMapModeChanged(object? s, MapMode e) => RenderImage();

      private void RenderOnEvent(object? s, List<Province> e) => RenderImage();

      private void OnFormClose(object? sender, EventArgs e)
      {
         Selection.OnProvinceGroupDeselected -= RenderOnEvent;
         Selection.OnProvinceGroupSelected -= RenderOnEvent;
         MapModeManager.MapModeChanged -= OnMapModeChanged;
      }

      private void SelectFolderButton(object sender, EventArgs e)
      {
         IO.OpenFolderDialog(Globals.Settings.Saving.MapModeExportPath, "select a folder where to save the image", out var path);
         Globals.Settings.Saving.MapModeExportPath = path;
      }

      private void RenderImage()
      {
         MapDrawing.Clear(ZoomControl, Color.DimGray);
         var rectangle = Rectangle.Empty;
         foreach (var layer in layers)
         {
            var map = layer.RenderToMap(ZoomControl);
            if (rectangle == Rectangle.Empty)
               rectangle = map;
            rectangle = Geometry.GetBounds(rectangle, map);
         }
         if (Selection.Count == 0)
            return;
         ZoomControl.FocusOn(rectangle);
         ZoomControl.Invalidate();
      }

      private void SaveButton_Click(object sender, EventArgs e)
      {
         if (string.IsNullOrWhiteSpace(PathTextBox.Text) || LayerListView.Items.Count == 0)
            return;
         using (var map = ZoomControl.Map)
         {
            var path = Path.Combine(Globals.Settings.Saving.MapModeExportPath, PathTextBox.Text + ".png");
            if (!Directory.Exists(Path.GetDirectoryName(path)))
               MessageBox.Show("The directory does not exist", "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
            else
            {
               switch (GetImageSize())
               {
                  case ImageSize.ImageSizeSelection:
                     var bounds = Geometry.GetBounds(Selection.GetSelectedProvinces);
                     var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
                     using (var graphics = Graphics.FromImage(bitmap))
                        graphics.DrawImage(map, bounds with
                        {
                           X = 0,
                           Y = 0
                        }, bounds, GraphicsUnit.Pixel);
                     bitmap.Save(path, ImageFormat.Png);
                     bitmap?.Dispose();
                     break;
                  case ImageSize.ImageSizeOriginal:
                     map.Save(path, ImageFormat.Png);
                     break;
               }
            }
         }
      }

      private ImageSize GetImageSize()
      {
         ImageSize result;
         return !Enum.TryParse<ImageSize>(ImageSizeBox.Text, true, out result) ? ImageSize.ImageSizeOriginal : result;
      }

      private DrawingOptions GetDrawingOption()
      {
         DrawingOptions result;
         return !Enum.TryParse<DrawingOptions>(OptionComboBox.Text, true, out result) ? DrawingOptions.Selection : result;
      }

      private void SelectionDrawerForm_FormClosing(object? sender, FormClosingEventArgs e)
      {
         ZoomControl.Dispose();
      }

      private void AddButton_Click(object sender, EventArgs e)
      {
         if (OptionComboBox.SelectedItem == null)
            return;
         LayerListView.Items.Add(new ListViewItem(OptionComboBox.Text));
         layers.Add(new DrawingLayer(GetDrawingOption()));
         RenderImage();
      }
   }

   public enum ImageSize
   {
      ImageSizeOriginal,
      ImageSizeSelection,

   }

   public enum DrawingOptions
   {
      Country,
      Area,
      Region,
      SuperRegion,
      ProvinceCollections,
      ColonialRegion,
      TradeNode,
      TradeCompanyRegion,
      Selection,
      NeighboringProvinces,
      NeighboringCountries,
      SeaProvinces,
      CoastalOutline,
      AllCoast,
      AllLand,
      AllSea,
      Everything,
   }

   public class DrawingLayer
   {
      private Func<List<Province>> ProvinceGetter;
      private DrawingOptions _options;

      private MapModeType mapMode;
      public MapModeType MapMode
      {
         get => mapMode;
         set
         {
            mapMode = value;
            _mode = MapModeManager.GetMapMode(value);
         }
      }

      private MapMode _mode;
      public RenderingSettings.BorderMergeType Style { get; set; } = RenderingSettings.BorderMergeType.Merge;
      public PixelsOrBorders pixelsOrBorders { get; set; } = PixelsOrBorders.Both;
      public byte Opacity { get; set; } = 0;
      public Color Shading { get; set; } = Color.White;
      public Color BorderColor { get; set; } = Color.Black;


      public DrawingOptions Options
      {
         get => _options;
         set
         {
            _options = value;
            SetProvinceGetter();
         }
      }

      public DrawingLayer(DrawingOptions options)
      {
         MapMode = MapModeType.Province;
         Options = options;
      }

      private void SetProvinceGetter()
      {
         switch (Options)
         {
            case DrawingOptions.Selection:
               ProvinceGetter = Selection.GetSelectedProvincesFunc;
               break;
            default:
               ProvinceGetter = Selection.GetSelectedProvincesFunc;
               break;
         }
      }

      private int GetColor(Province province)
      {
         var factor = Opacity / 255.0f;
         var inverse = 1f - factor;
         var color = Color.FromArgb(_mode.GetProvinceColor(province));
         var R = (byte)(color.R * inverse + Shading.R * factor);
         var G = (byte)(color.G * inverse + Shading.G * factor);
         var B = (byte)(color.B * inverse + Shading.B * factor);
         return (R << 16 | G << 8 | B);
      }

      public Rectangle RenderToMap(ZoomControl control)
      { 
         var provinceList = ProvinceGetter();
         Dictionary<Province, int> cache = new (provinceList.Count);
         //MapModeManager.ConstructClearCache(Globals.Provinces, _mode, cache);
         var defaultColor = Color.LightGray.ToArgb();
         foreach (var province in Globals.Provinces.Except(provinceList))
            cache[province] = defaultColor;
         MapModeManager.ConstructCache(provinceList, _mode, cache);


         switch (pixelsOrBorders)
         {
            case PixelsOrBorders.Pixels:
               MapDrawing.DrawOnMap(provinceList, GetColor, control, PixelsOrBorders.Both, cache);
               break;
            case PixelsOrBorders.Borders:
               MapDrawing.DrawOnMap(provinceList, BorderColor.ToArgb(), control, PixelsOrBorders.Borders, cache);
               break;
            case PixelsOrBorders.Both:
               MapDrawing.DrawOnMap(provinceList, GetColor, control, PixelsOrBorders.Pixels, cache);
               MapDrawing.DrawOnMap(provinceList, BorderColor.ToArgb(), control, PixelsOrBorders.Borders, cache);
               break;
         }

         
         return Geometry.GetBounds(provinceList);
      }

      public override string ToString() => Options.ToString();
   }

   public class PopUpForm : Form
   {
      private PropertyGrid _propGrid;

      public event EventHandler? PropertyChanged;

      public PopUpForm(object selectedObject)
      {
         var propertyGrid = new PropertyGrid();
         propertyGrid.Dock = DockStyle.Fill;
         _propGrid = propertyGrid;

         Controls.Add(_propGrid);
         _propGrid.SelectedObject = selectedObject;
         _propGrid.PropertyValueChanged += OnPropertyChanged;

         Text = selectedObject.GetType().Name;
      }

      public void OnPropertyChanged(object? sender, EventArgs e)
      {
         OnPropertyChanged();
      }

      protected virtual void OnPropertyChanged()
      {
         PropertyChanged?.Invoke(this, EventArgs.Empty);
      }
   }
}

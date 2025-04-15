using System;
using System.Collections.Generic;
using System.Diagnostics;
using CafeMaestro.Models;
using CafeMaestro.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Storage;

namespace CafeMaestro
{
    public partial class RoastImportPage : ContentPage
    {
        // Using private field with nullable type to properly handle null case
        private readonly RoastDataService? _roastDataService;
        private List<string> _csvHeaders = new List<string>();
        private string _selectedFilePath = string.Empty;

        public RoastImportPage()
        {
            InitializeComponent();
            
            // Initialize service in constructor to avoid null warning
            if (App.Current?.Handler?.MauiContext != null)
            {
                _roastDataService = App.Current.Handler.MauiContext.Services.GetService<RoastDataService>();
                
                if (_roastDataService == null)
                {
                    DisplayAlert("Error", "Could not initialize required services", "OK");
                }
            }
        }

        private async void BrowseButton_Clicked(object sender, EventArgs e)
        {
            try
            {
                // Show loading state
                LoadingIndicator.IsVisible = true;
                LoadingIndicator.IsRunning = true;
                ImportButton.IsEnabled = false;
                FileStatusLabel.Text = "Opening file picker...";

                var fileResult = await FilePicker.PickAsync(new PickOptions
                {
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, new[] { ".csv" } },
                        { DevicePlatform.macOS, new[] { "csv" } },
                        { DevicePlatform.iOS, new[] { "public.comma-separated-values-text" } },
                        { DevicePlatform.Android, new[] { "text/csv" } }
                    }),
                    PickerTitle = "Select a CSV file with roast data"
                });

                if (fileResult == null)
                {
                    // User canceled
                    FileStatusLabel.Text = "No file selected";
                    MapFieldsSection.IsVisible = false;
                    PreviewSection.IsVisible = false;
                    ImportButton.IsEnabled = false;
                    return;
                }

                _selectedFilePath = fileResult.FullPath;
                FilePathEntry.Text = _selectedFilePath;
                FileStatusLabel.Text = "Reading headers from file...";

                // Read CSV headers - use static method directly
                _csvHeaders = await RoastDataService.GetCsvHeadersAsync(_selectedFilePath);

                if (_csvHeaders.Count == 0)
                {
                    FileStatusLabel.Text = "Error: Could not find CSV headers in the file.";
                    MapFieldsSection.IsVisible = false;
                    PreviewSection.IsVisible = false;
                    return;
                }

                // Setup field mapping
                SetupFieldMappings();
                FileStatusLabel.Text = $"Found {_csvHeaders.Count} columns in CSV file.";
                MapFieldsSection.IsVisible = true;
                PreviewSection.IsVisible = true;
                ImportButton.IsEnabled = true;

                // Try to auto-map columns based on header names
                AutoMapColumns();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Error reading file: {ex.Message}", "OK");
                FileStatusLabel.Text = $"Error: {ex.Message}";
            }
            finally
            {
                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;
            }
        }

        private void SetupFieldMappings()
        {
            var pickers = new[] { DatePicker, CoffeePicker, TempPicker, TimePicker, BatchWeightPicker, FinalWeightPicker, LossPicker, NotesPicker };

            foreach (var picker in pickers)
            {
                picker.Items.Clear();
                picker.Items.Add("(None)");
                
                foreach (var header in _csvHeaders)
                {
                    picker.Items.Add(header);
                }
                
                picker.SelectedIndex = 0;
            }
        }

        private void AutoMapColumns()
        {
            // Map common header names to appropriate pickers
            foreach (var header in _csvHeaders)
            {
                string lowerHeader = header.ToLower();
                
                if (lowerHeader.Contains("date"))
                {
                    DatePicker.SelectedItem = header;
                }
                else if (lowerHeader.Contains("coffee") || lowerHeader.Contains("bean") || lowerHeader == "type")
                {
                    CoffeePicker.SelectedItem = header;
                }
                else if (lowerHeader.Contains("temp"))
                {
                    TempPicker.SelectedItem = header;
                }
                else if (lowerHeader.Contains("time"))
                {
                    TimePicker.SelectedItem = header;
                }
                else if (lowerHeader.Contains("batch") || lowerHeader == "weight (g)")
                {
                    BatchWeightPicker.SelectedItem = header;
                }
                else if (lowerHeader.Contains("final"))
                {
                    FinalWeightPicker.SelectedItem = header;
                }
                else if (lowerHeader.Contains("loss") || lowerHeader.Contains("%"))
                {
                    LossPicker.SelectedItem = header;
                }
                else if (lowerHeader.Contains("note"))
                {
                    NotesPicker.SelectedItem = header;
                }
            }

            UpdatePreview();
        }

        private void UpdatePreview()
        {
            // Show the mapping selections
            int mappedFields = 0;
            var mappings = GetMappings();
            
            foreach (var map in mappings)
            {
                if (!string.IsNullOrEmpty(map.Value))
                {
                    mappedFields++;
                }
            }
            
            PreviewStatusLabel.Text = $"Mapped {mappedFields} out of 8 fields. Ready to import.";
        }

        private Dictionary<string, string> GetMappings()
        {
            var mappings = new Dictionary<string, string>();
            
            // Get the selected column for each property
            string GetSelectedColumn(Picker picker) => picker.SelectedIndex > 0 ? picker.Items[picker.SelectedIndex] : "";
            
            mappings["RoastDate"] = GetSelectedColumn(DatePicker);
            mappings["BeanType"] = GetSelectedColumn(CoffeePicker);
            mappings["Temperature"] = GetSelectedColumn(TempPicker);
            mappings["RoastTime"] = GetSelectedColumn(TimePicker);
            mappings["BatchWeight"] = GetSelectedColumn(BatchWeightPicker);
            mappings["FinalWeight"] = GetSelectedColumn(FinalWeightPicker);
            mappings["WeightLoss"] = GetSelectedColumn(LossPicker);
            mappings["Notes"] = GetSelectedColumn(NotesPicker);
            
            return mappings;
        }

        private async void ImportButton_Clicked(object sender, EventArgs e)
        {
            try
            {
                // Validate required fields
                if (DatePicker.SelectedIndex <= 0 || CoffeePicker.SelectedIndex <= 0)
                {
                    await DisplayAlert("Required Fields Missing", "Date and Coffee Bean fields are required for import.", "OK");
                    return;
                }

                // Show loading indicator
                LoadingIndicator.IsVisible = true;
                LoadingIndicator.IsRunning = true;
                ImportButton.IsEnabled = false;
                CancelButton.IsEnabled = false;
                PreviewStatusLabel.Text = "Importing data...";

                // Get all the mappings
                var mappings = GetMappings();

                // Import the roasts
                if (_roastDataService != null)
                {
                    // Import the data - the ImportRoastsFromCsvAsync method already handles duplicates
                    var result = await _roastDataService.ImportRoastsFromCsvAsync(_selectedFilePath, mappings);

                    // Show result
                    if (result.Failed > 0)
                    {
                        // Show detailed error message with the first few errors
                        string errorDetails = string.Join("\n", result.Errors.Take(5));
                        if (result.Errors.Count > 5)
                        {
                            errorDetails += $"\n...and {result.Errors.Count - 5} more errors.";
                        }

                        await DisplayAlert("Import Results", 
                            $"Successfully imported {result.Success} roast logs.\n" +
                            $"Failed to import {result.Failed} roast logs.\n\n" +
                            $"Error details:\n{errorDetails}", "OK");
                    }
                    else
                    {
                        await DisplayAlert("Import Successful", $"Successfully imported {result.Success} roast logs!", "OK");
                    }
                }

                // Navigate back to RoastLogPage after import
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Import Error", $"Error during import: {ex.Message}", "OK");
                Debug.WriteLine($"Import error: {ex}");
            }
            finally
            {
                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;
                ImportButton.IsEnabled = true;
                CancelButton.IsEnabled = true;
            }
        }

        private async void CancelButton_Clicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
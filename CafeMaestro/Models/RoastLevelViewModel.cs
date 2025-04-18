using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace CafeMaestro.Models
{
    public class RoastLevelViewModel : INotifyPropertyChanged, IConvertible
    {
        private Guid _id;
        private string _name = string.Empty;
        private double _minWeightLossPercentage;
        private double _maxWeightLossPercentage;

        public Guid Id 
        { 
            get => _id; 
            set
            {
                if (_id != value)
                {
                    _id = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Name 
        { 
            get => _name; 
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public double MinWeightLossPercentage 
        { 
            get => _minWeightLossPercentage; 
            set
            {
                if (_minWeightLossPercentage != value)
                {
                    _minWeightLossPercentage = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayRange));
                }
            }
        }

        public double MaxWeightLossPercentage 
        { 
            get => _maxWeightLossPercentage; 
            set
            {
                if (_maxWeightLossPercentage != value)
                {
                    _maxWeightLossPercentage = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayRange));
                }
            }
        }

        public string DisplayRange => $"{MinWeightLossPercentage:F1}% - {MaxWeightLossPercentage:F1}% weight loss";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Convert from model to view model
        public static RoastLevelViewModel FromModel(RoastLevelData model)
        {
            return new RoastLevelViewModel
            {
                Id = model.Id,
                Name = model.Name,
                MinWeightLossPercentage = model.MinWeightLossPercentage,
                MaxWeightLossPercentage = model.MaxWeightLossPercentage
            };
        }

        // Convert from view model to model
        public RoastLevelData ToModel()
        {
            return new RoastLevelData
            {
                Id = this.Id,
                Name = this.Name,
                MinWeightLossPercentage = this.MinWeightLossPercentage,
                MaxWeightLossPercentage = this.MaxWeightLossPercentage
            };
        }

        #region IConvertible Implementation
        // Implement IConvertible interface to fix navigation parameter serialization
        public TypeCode GetTypeCode()
        {
            return TypeCode.Object;
        }

        public bool ToBoolean(IFormatProvider? provider)
        {
            throw new InvalidCastException("Cannot convert RoastLevelViewModel to Boolean");
        }

        public byte ToByte(IFormatProvider? provider)
        {
            throw new InvalidCastException("Cannot convert RoastLevelViewModel to Byte");
        }

        public char ToChar(IFormatProvider? provider)
        {
            throw new InvalidCastException("Cannot convert RoastLevelViewModel to Char");
        }

        public DateTime ToDateTime(IFormatProvider? provider)
        {
            throw new InvalidCastException("Cannot convert RoastLevelViewModel to DateTime");
        }

        public decimal ToDecimal(IFormatProvider? provider)
        {
            throw new InvalidCastException("Cannot convert RoastLevelViewModel to Decimal");
        }

        public double ToDouble(IFormatProvider? provider)
        {
            throw new InvalidCastException("Cannot convert RoastLevelViewModel to Double");
        }

        public short ToInt16(IFormatProvider? provider)
        {
            throw new InvalidCastException("Cannot convert RoastLevelViewModel to Int16");
        }

        public int ToInt32(IFormatProvider? provider)
        {
            throw new InvalidCastException("Cannot convert RoastLevelViewModel to Int32");
        }

        public long ToInt64(IFormatProvider? provider)
        {
            throw new InvalidCastException("Cannot convert RoastLevelViewModel to Int64");
        }

        public sbyte ToSByte(IFormatProvider? provider)
        {
            throw new InvalidCastException("Cannot convert RoastLevelViewModel to SByte");
        }

        public float ToSingle(IFormatProvider? provider)
        {
            throw new InvalidCastException("Cannot convert RoastLevelViewModel to Single");
        }

        public string ToString(IFormatProvider? provider)
        {
            return Name;
        }

        public object ToType(Type conversionType, IFormatProvider? provider)
        {
            if (conversionType == typeof(RoastLevelViewModel))
                return this;
                
            throw new InvalidCastException($"Cannot convert RoastLevelViewModel to {conversionType}");
        }

        public ushort ToUInt16(IFormatProvider? provider)
        {
            throw new InvalidCastException("Cannot convert RoastLevelViewModel to UInt16");
        }

        public uint ToUInt32(IFormatProvider? provider)
        {
            throw new InvalidCastException("Cannot convert RoastLevelViewModel to UInt32");
        }

        public ulong ToUInt64(IFormatProvider? provider)
        {
            throw new InvalidCastException("Cannot convert RoastLevelViewModel to UInt64");
        }
        #endregion
    }
}
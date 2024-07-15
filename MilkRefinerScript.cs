using System;
using MasterSCADA.Script.FB;
using MasterSCADA.Hlp;
using FB;
using FB.FBAttributes;
using System.Linq;

[FBRetain]
public partial class ФБ : ScriptBase
{
	Tank RawMilk;
	Tank Extender;
	Tank HeatEXCR_Water;
	Tank HeatEXCR_Milk;
	Tank RawWater;
	Tank Stabilizer;
	Tank CoolMilk;
	
	bool mainFlow;
	int coolingtacts;
	bool repeatflow;
	bool started;
	
	//Вызывается при включении устройства
	public override void Start()
	{
		RawMilk = new Tank(V_RawMilk, RawMilkAmount, RawMilkTemp);
		Extender = new Tank(V_Extender, 0, 0);
		HeatEXCR_Water = new Tank(V_HeatEXCR_Water, 0, 0);
		HeatEXCR_Milk = new Tank(V_HeatEXCR_Milk, 0, 0);
		RawWater = new Tank(V_RawWater,RawWaterAmount, 1);
		Stabilizer = new Tank(V_Stabilizer, 0, 0);
		CoolMilk = new Tank(V_CoolMilk, 0, 0);
		Pump1_on = false;
		Pump2_on = false;
		mainFlow = true;
		Flow1 = 0; Temp1 = 0;
		Flow2 = 0; Temp2 = 0;
		Flow3 = 0; Temp3 = 0;
		Flow4 = 0; Temp4 = 0;
		Flow5 = 0; Temp5 = 0;
		Flow6 = 0; Temp6 = 0;
		Flow7 = 0; Temp7 = 0;
		Flow8 = 0; Temp8 = 0;
		Flow9 = 0; Temp9 = 0;
		coolingtacts = Convert.ToInt32(((RawMilkTemp - CoolMilkTemp) / CoolingValue));
		
		Operating = false;
		started = false;
	}
	//Вызывается при каждом опросе
    public override void Execute()
    {
    	if(L_CoolMilk >= RawMilkAmount - 0.05)
			Operating = false;
    	
    	CoolWater();
    	HandleFlows();
    	HandleOutput();
    	HandleDevices();
    	
    	
    }
    
    //Обрабатывает потоки
    public void HandleFlows()
    {
    	if(MainValve == true)
    	{
    		Flow1 = Extender.FlowFrom(Flow, RawMilk, out mainFlow);
    		Temp1 = Flow1 > 0 ? RawMilk.Temperature : 0;
    		
    		if(started == false)
    		{
    			Operating = true;
    			started = true;
    		}
    	}
    	
    	if(repeatflow == true && !mainFlow)
    	{
    		Flow5 = Extender.FlowFrom(Flow, CoolMilk);
    		Temp5 = Flow1 > 0 ? CoolMilk.Temperature : 0;
    	}
    		
		//Если в расширителе находится молоко
		if(Extender.PercentFilled > 60 || (RawMilk.isEmpty && !Extender.isEmpty) || (!Extender.isEmpty && MainValve == false))
		{
			Flow2 = HeatEXCR_Milk.FlowFrom(Flow, Extender);
			Temp2 = Flow2 > 0 ? Extender.Temperature : 0;
		}
		else
		{
			Flow2 = 0;
			Temp2 = 0;
			
			Flow6 = 0;
			Temp6 = 0;
		}
		
	
		//Если в теплообменнике находится молоко - включить воду
		if(HeatEXCR_Milk.isFull || (RawMilk.isEmpty && !HeatEXCR_Milk.isEmpty) || (MainValve == false && !HeatEXCR_Milk.isEmpty))
		{
			Flow7 = HeatEXCR_Water.FlowFrom(Flow, RawWater);
			Temp7 = Flow7 > 0 ? RawWater.Temperature : 0;
			
			if(Flow7 > 0)
				Pump1_on = true;
			else
				Pump1_on = false;
				
			if(HeatEXCR_Milk.Temperature > 4.0)
			{
				ExchangeHeat();
			}
			else
			{
				Flow3 = Stabilizer.FlowFrom(Flow, HeatEXCR_Milk);
				Temp3 = Flow3 > 0 ? HeatEXCR_Milk.Temperature : 0;
				
				Flow6 = RawWater.FlowFrom(Flow, HeatEXCR_Water, RawWater.Temperature);
				Temp6 = Flow6 > 0 ? HeatEXCR_Water.Temperature : 0;
				
				RawWater.Temperature = ((Flow6 * Temp6 + RawWater.Level * RawWater.Temperature) / (Flow6 + RawWater.Level));
				
			}
		}
		else
		{
			Flow3 = 0;
			Temp3 = 0;	
		}
		
		
		if(Stabilizer.Level >= StabilizerOutput || RawMilk.isEmpty || (!Stabilizer.isEmpty && MainValve == false))
		{
			if(CoolMilk.Level == 0)
			{
				Flow4 = CoolMilk.FlowFrom(StabilizerOutput, Stabilizer);
				Temp4 = Flow4 > 0 ? Stabilizer.Temperature : 0;
			}
			else
			{
				Flow4 = CoolMilk.FlowFrom(StabilizerOutput, Stabilizer, CoolMilk.Temperature);
				Temp4 = Flow4 > 0 ? Stabilizer.Temperature : 0;
			
				CoolMilk.Temperature = ((Flow4 * Temp4 + CoolMilk.Temperature * CoolMilk.Level)/(CoolMilk.Level + Flow4));
			}
			
		}
		else
		{
			Flow4 = 0;
			Temp4 = 0;
		}
		
		
    }
    
    //Функция, обменивающая теплоту в теплообменнике
    public void ExchangeHeat()
    {
    	Flow3 = 0;
    	Temp3 = 0;
    	Flow6 = 0;
		Temp6 = 0;	
    	
    	double? WarmingValue = 13 / coolingtacts;
    	
    	HeatEXCR_Milk.Cool(CoolingValue, CoolMilkTemp);
    	HeatEXCR_Water.Warm(WarmingValue, 14);	
    }
    
    public void CoolWater()
    {
    	if(RawWater.Temperature > 1)
    	{
    		
    		Flow8 = Flow*2;
    		Flow9 = Flow8;
    		
    		Temp8 = RawWater.Temperature;
    		Temp9 = CoolingTemp;
    		
    		Pump2_on = true;
    		
    		RawWater.Temperature = ((RawWater.Temperature * RawWater.Level + Flow9*Temp9)/(RawWater.Level + Flow9));
    		
    		if(RawWater.Temperature < 0)
    			RawWater.Temperature = 1;
    	}
    	else
    	{
    		Flow8 = 0;
    		Flow9 = 0;
    		Temp8 = 0;
    		Temp9 = 0;
    		
    		Pump2_on = false;
    		
    	}
    }
    
    //Обрабатывает выходы кнопок управления
    public void HandleDevices()
    {
    	MainValveBlock = MainValve == true ? !mainFlow : false;
    	if(MainValveBlock == true)
    	{
    		MainValve_on = false;
    	}
    	else
    	{
    		MainValve_on = MainValve;
    	}
    }
    
    //Обработать выходы переменных
    public void HandleOutput()
    {
    	T_RawMilk = RawMilk.Temperature;
    	T_Extender = Extender.Temperature;
    	T_HeatEXCR_Water = HeatEXCR_Water.Temperature;
    	T_HeatEXCR_Milk = HeatEXCR_Milk.Temperature;
    	T_RawWater = RawWater.Temperature;
    	T_Stabilizer = Stabilizer.Temperature;
    	T_CoolMilk = CoolMilk.Temperature;
    	L_RawMilk = RawMilk.Level;
    	L_Extender = Extender.Level;
    	L_HeatEXCR_Water = HeatEXCR_Water.Level;
    	L_HeatEXCR_Milk = HeatEXCR_Milk.Level;
    	L_RawWater = RawWater.Level;
    	L_Stabilizer = Stabilizer.Level;
    	L_CoolMilk = CoolMilk.Level;
    }
    
    public void WarmUpMilk()
    {
    	if(CoolMilk.Level > 0)
    		CoolMilk.Warm(MilkWarmingUpValue);
    		
    	if(CoolMilk.Temperature >= 6)
    		repeatflow = true;
    	else
    		repeatflow = false;
    }
}

//Класс, определяющий емкость
public class Tank
{
	private double? capacity;	//Объем емкости
	private double? level;		//Уровень емкости
	private double? temperature;	//Температура емкости
	
	//Поля для чтения
	public double? Capacity {get {return capacity;}	}
	public double? Level{get {return level;} set{level = value;}}
	public double? Temperature {get {return temperature;} set {temperature = value;}}
	
	public double? PercentFilled
	{
		get 
		{
			return (level/capacity)*100;
		}
	}
	
	
	public bool isEmpty
	{
		get
		{
			if(level <= 0)
				return true;
			else
				return false;
		}
		
	}
	
	public bool isFull
	{
		get
		{
			if(level >= capacity)
				return true;
			else
				return false;
		}
	}
	
	
	//Конструктор с параметрами, создающий емкость
	public Tank(double? capacity, double? level, double? temperature)
	{
		this.capacity = capacity;
		this.level = level;
		this.temperature = temperature;
	}
	
	
	
	//Рассчитать объем, который можно вычесть от уровня
	private double FlowOutPossible(double? value)
	{
		if(level - value >= 0)
			return Convert.ToDouble(value);
		else
		{
			double? safeflow = level;
			return Convert.ToDouble(safeflow);
		}
	}
	
	//Рассчитать объем, который можно прибавить к уровню
	private double FlowInPossible(double? value)
	{
		if(level + value <= capacity)
			return Convert.ToDouble(value);
		else
		{
			double? safeflow = capacity-level;
			return Convert.ToDouble(safeflow);
		}
	}
	
	
	
	//Вычислить приемлемый для двух емкостей объем и выполнить переливание
	public double? FlowFrom(double? value, Tank from, double? temperature = null)
	{
		double SafeValue = Math.Min(FlowInPossible(value), from.FlowOutPossible(value));
		
		if(SafeValue > 0)
		{
			level += SafeValue;
			from.Level -= SafeValue;
			
			if(temperature == null)
				this.temperature = from.temperature;
			else
				this.temperature = temperature;
				
			return SafeValue;
		}
		else
		{
			return 0;
		}		
	}
	
	//Вычислить приемлемый для двух емкостей объем и выполнить переливание
	//Назначить выходной логический параметр истинным, если существует поток
	public double? FlowFrom(double? value, Tank from, out bool flows, double? temperature = null)
	{
		double SafeValue = Math.Min(FlowInPossible(value), from.FlowOutPossible(value));
		
		if(SafeValue > 0)
		{
			level += SafeValue;
			from.Level -= SafeValue;
			
			if(temperature == null)
				this.temperature = from.temperature;
			else
				this.temperature = temperature;
			
			flows = true;
			return SafeValue;
		}
		else
		{
			flows = false;
			return 0;
		}	
	}
	
	public void Cool(double? value, double? restrict = 0)
	{
		if(restrict != 0)
		{
			if(temperature - value < restrict)
				this.temperature = restrict;
			else
				this.temperature -= value;
		}
		else
			this.temperature -= value;
	}
	
	public void Warm(double? value, double? restrict = 0)
	{
		if(restrict != 0)
		{
			if(temperature + value > restrict)
				this.temperature = restrict;
			else
				this.temperature += value;
		}
		else
			this.temperature += value;
	}
}
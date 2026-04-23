package cards

import "testing"

func TestParseCardVars_Scythe(t *testing.T) {
	body := []byte(`
		private const string _increaseKey = "Increase";
		private const int _baseDamage = 13;
		private int _currentDamage = 13;
		private int _increasedDamage;
		public int CurrentDamage
		{
			get { return _currentDamage; }
			set { }
		}
		public int IncreasedDamage
		{
			get { return _increasedDamage; }
			set { }
		}
		protected override IEnumerable<DynamicVar> CanonicalVars => new global::<>z__ReadOnlyArray<DynamicVar>(new DynamicVar[2]
		{
			new DamageVar(CurrentDamage, ValueProp.Move),
			new IntVar("Increase", 3m)
		});
		protected override void OnUpgrade()
		{
			base.DynamicVars["Increase"].UpgradeValueBy(1m);
		}
	`)
	base, up := parseCardVars(body)
	if base["Damage"] != 13 || base["Increase"] != 3 {
		t.Fatalf("base vars wrong: %v", base)
	}
	if up["Damage"] != 13 || up["Increase"] != 4 {
		t.Fatalf("upgraded vars wrong: %v", up)
	}
}

func TestParseCardVars_Abrasive(t *testing.T) {
	body := []byte(`
		protected override IEnumerable<DynamicVar> CanonicalVars => new global::<>z__ReadOnlyArray<DynamicVar>(new DynamicVar[2]
		{
			new PowerVar<DexterityPower>(1m),
			new PowerVar<ThornsPower>(2m)
		});
		protected override void OnUpgrade()
		{
			base.DynamicVars["ThornsPower"].UpgradeValueBy(1m);
		}
	`)
	base, up := parseCardVars(body)
	if base["DexterityPower"] != 1 || base["ThornsPower"] != 2 {
		t.Fatalf("base vars wrong: %v", base)
	}
	if up["ThornsPower"] != 3 || up["DexterityPower"] != 1 {
		t.Fatalf("upgraded vars wrong: %v", up)
	}
}

func TestParseCardVars_Guilty_NoUpgrade(t *testing.T) {
	body := []byte(`
		public override int MaxUpgradeLevel => 0;
		protected override IEnumerable<DynamicVar> CanonicalVars => new global::<>z__ReadOnlySingleElementList<DynamicVar>(new DynamicVar("Combats", 5m));
	`)
	base, up := parseCardVars(body)
	if base["Combats"] != 5 {
		t.Fatalf("base vars wrong: %v", base)
	}
	if up != nil {
		t.Fatalf("expected no upgradedVars, got %v", up)
	}
}

func TestParseCardVars_TypedUpgradeAccessor(t *testing.T) {
	body := []byte(`
		protected override IEnumerable<DynamicVar> CanonicalVars => new global::<>z__ReadOnlySingleElementList<DynamicVar>(new DamageVar(6m, ValueProp.Move));
		protected override void OnUpgrade()
		{
			base.DynamicVars.Damage.UpgradeValueBy(3m);
		}
	`)
	base, up := parseCardVars(body)
	if base["Damage"] != 6 || up["Damage"] != 9 {
		t.Fatalf("wrong vars: base=%v up=%v", base, up)
	}
}

func TestParseCardVars_SingleElementList(t *testing.T) {
	body := []byte(`
		protected override IEnumerable<DynamicVar> CanonicalVars => new global::<>z__ReadOnlySingleElementList<DynamicVar>(new PowerVar<VulnerablePower>(2m));
	`)
	base, _ := parseCardVars(body)
	if base["VulnerablePower"] != 2 {
		t.Fatalf("wrong: %v", base)
	}
}

func TestParseCardVars_NoCanonicalVars(t *testing.T) {
	body := []byte(`public Strike() : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy) {}`)
	base, up := parseCardVars(body)
	if base != nil || up != nil {
		t.Fatalf("expected no vars, got base=%v up=%v", base, up)
	}
}

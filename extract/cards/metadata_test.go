package cards

import "testing"

func TestParseStarCost_Override(t *testing.T) {
	// FallingStar overrides CanonicalStarCost; energy cost lives separately
	// in the base(...) ctor and must not be confused for it.
	body := []byte(`
		public override int CanonicalStarCost => 2;
		protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[1]
		{
			new DamageVar(8m, ValueProp.Move)
		};
		public FallingStar()
			: base(0, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
		{
		}
	`)
	if got := parseStarCost(body); got != 2 {
		t.Fatalf("FallingStar star cost = %d, want 2", got)
	}
}

func TestParseStarCost_None(t *testing.T) {
	// A card with no CanonicalStarCost override has no star cost (0), even
	// though it has an energy cost.
	body := []byte(`
		public Bash()
			: base(2, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
		{
		}
	`)
	if got := parseStarCost(body); got != 0 {
		t.Fatalf("Bash star cost = %d, want 0", got)
	}
}

package cardtext

import (
	"reflect"
	"testing"
)

func TestBash(t *testing.T) {
	got := Parse("Deal {Damage:diff()} damage.\nApply {VulnerablePower:diff()} [gold]Vulnerable[/gold].")
	want := []Line{
		{
			{Text: "Deal ", Style: StyleNormal},
			{Text: "?", Style: StylePlaceholder, Field: "Damage"},
			{Text: " damage.", Style: StyleNormal},
		},
		{
			{Text: "Apply ", Style: StyleNormal},
			{Text: "?", Style: StylePlaceholder, Field: "VulnerablePower"},
			{Text: " ", Style: StyleNormal},
			{Text: "Vulnerable", Style: StyleHighlight},
			{Text: ".", Style: StyleNormal},
		},
	}
	if !reflect.DeepEqual(got, want) {
		t.Fatalf("Parse(BASH) mismatch\n  got:  %#v\n  want: %#v", got, want)
	}
}

func TestDemonForm(t *testing.T) {
	got := Parse("At the start of your turn, gain {StrengthPower:diff()} [gold]Strength[/gold].")
	want := []Line{
		{
			{Text: "At the start of your turn, gain ", Style: StyleNormal},
			{Text: "?", Style: StylePlaceholder, Field: "StrengthPower"},
			{Text: " ", Style: StyleNormal},
			{Text: "Strength", Style: StyleHighlight},
			{Text: ".", Style: StyleNormal},
		},
	}
	if !reflect.DeepEqual(got, want) {
		t.Fatalf("Parse(DEMON_FORM) mismatch\n  got:  %#v\n  want: %#v", got, want)
	}
}

func TestDominate(t *testing.T) {
	got := Parse("Apply {VulnerablePower:diff()} [gold]Vulnerable[/gold].\nGain {StrengthPerVulnerable:diff()} [gold]Strength[/gold] for each [gold]Vulnerable[/gold] on the enemy.")
	want := []Line{
		{
			{Text: "Apply ", Style: StyleNormal},
			{Text: "?", Style: StylePlaceholder, Field: "VulnerablePower"},
			{Text: " ", Style: StyleNormal},
			{Text: "Vulnerable", Style: StyleHighlight},
			{Text: ".", Style: StyleNormal},
		},
		{
			{Text: "Gain ", Style: StyleNormal},
			{Text: "?", Style: StylePlaceholder, Field: "StrengthPerVulnerable"},
			{Text: " ", Style: StyleNormal},
			{Text: "Strength", Style: StyleHighlight},
			{Text: " for each ", Style: StyleNormal},
			{Text: "Vulnerable", Style: StyleHighlight},
			{Text: " on the enemy.", Style: StyleNormal},
		},
	}
	if !reflect.DeepEqual(got, want) {
		t.Fatalf("Parse(DOMINATE) mismatch\n  got:  %#v\n  want: %#v", got, want)
	}
}

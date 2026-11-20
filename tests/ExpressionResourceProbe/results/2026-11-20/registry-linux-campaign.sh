#!/usr/bin/env bash
# Differential registry campaign on Linux (peak runs and refusals, no timing).
# Usage: linux-campaign.sh <probe directory> <output file>
set -u
probe="$1/ExpressionResourceProbe"
out="$2"
chmod +x "$probe"
: > "$out"
run() {
    result=$("$probe" parser-registry "$@")
    echo "$? $result" >> "$out"
}
for spec in "csyntax-sum 1000" "csyntax-sum 5000" "csyntax-mixed 1000" "vbsyntax-sum 1000" "vbsyntax-sum 5000" \
            "vbsyntax-mixed 1000" "csyntax-parens 15" "g-alternatives 2000" "g-prefix 2000" "g-leftrec-sum 2000" \
            "g-leftrec-mixed 1000" "g-stateful 1000"; do
    run $spec --peak
    run $spec --peak --diagnostics
done
for spec in "g-ambiguous 150" "g-deep 300"; do
    run $spec --peak --thread stack:262144
    run $spec --peak --diagnostics --thread stack:262144
done
for limit in 150279 150278 170301 170300 30000 1000 1; do
    run csyntax-sum 1000 --max-retained $limit --peak
    run csyntax-sum 1000 --max-retained $limit --peak --diagnostics
done
for limit in 26689 26688 25354 25353 5000 500; do
    run g-stateful 1000 --max-retained $limit --peak
    run g-stateful 1000 --max-retained $limit --peak --diagnostics
done
for limit in 682265 682264 747429 747428 40000; do
    run csyntax-mixed 1000 --max-retained $limit --peak
    run csyntax-mixed 1000 --max-retained $limit --peak --diagnostics
done

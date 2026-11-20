#!/usr/bin/env bash
# Differential look-ahead cache campaign on Linux (peak, look-ahead and breakdown runs and refusals, no timing).
# Usage: lookahead-linux-campaign.sh <probe directory> <output file>
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
            "g-leftrec-mixed 1000" "g-epsilon 1000" "g-stateful 1000"; do
    for mode in --peak --lookahead --breakdown; do
        run $spec $mode
        run $spec $mode --diagnostics
    done
done
for spec in "g-ambiguous 150" "g-deep 300"; do
    for mode in --peak --lookahead --breakdown; do
        run $spec $mode --thread stack:262144
        run $spec $mode --diagnostics --thread stack:262144
    done
done
for limit in 150279 150278 170301 170300 30000 1000 1; do
    run csyntax-sum 1000 --max-retained $limit --lookahead
    run csyntax-sum 1000 --max-retained $limit --lookahead --diagnostics
done
for limit in 119165 119164 141169 141168 47765 20000; do
    run vbsyntax-sum 1000 --max-retained $limit --lookahead
    run vbsyntax-sum 1000 --max-retained $limit --lookahead --diagnostics
done
for limit in 26689 26688 25354 25353 5000 500; do
    run g-stateful 1000 --max-retained $limit --lookahead
    run g-stateful 1000 --max-retained $limit --lookahead --diagnostics
done
for limit in 15999 15998 7000; do
    run g-leftrec-sum 2000 --max-retained $limit --lookahead
    run g-leftrec-sum 2000 --max-retained $limit --lookahead --diagnostics
done
for limit in 682265 682264 747429 747428 40000 10000; do
    run csyntax-mixed 1000 --max-retained $limit --lookahead
    run csyntax-mixed 1000 --max-retained $limit --lookahead --diagnostics
done

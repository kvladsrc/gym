#!/usr/bin/env bats

RUN="bazel run :sqrt"

assert_approx() {
    local result="$1"
    local expected="$2"
    local diff
    diff="$(awk -v a="$result" -v b="$expected" 'BEGIN {print (a>b)?a-b:b-a}')"
    if [ "$(awk -v d="$diff" 'BEGIN {print (d<=1e-4)?0:1}')" -ne 0 ]; then
        echo "expected ≈ $expected, got $result (|Δ|=$diff)" >&2
        return 1
    fi
}

@test "perfect square (4 → 2)" {
    result="$(echo '4' | $RUN)"
    [ "$result" = "2" ]
}

@test "perfect square (9 → 3)" {
    result="$(echo '9' | $RUN)"
    [ "$result" = "3" ]
}

@test "perfect square (144 → 12)" {
    result="$(echo '144' | $RUN)"
    [ "$result" = "12" ]
}

@test "non-perfect square (2 → 1.4142…)" {
    result="$(echo '2' | $RUN)"
    assert_approx "$result" "1.41421"
}

@test "small number (0.25 → 0.5)" {
    result="$(echo '0.25' | $RUN)"
    assert_approx "$result" "0.5"
}

@test "zero" {
    result="$(echo '0' | $RUN)"
    [ "$result" = "0" ]
}

@test "negative input → NaN" {
    result="$(echo '-4' | $RUN)"
    [[ $result =~ ([Nn][Aa][Nn]) ]]
}

@test "large number (1e6 → 1e3)" {
    result="$(echo '1000000' | $RUN)"
    assert_approx "$result" "1000"
}

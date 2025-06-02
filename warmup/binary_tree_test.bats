#!/usr/bin/env bats

RUN="bazel run :binary_tree"

@test "empty" {
    input=$'0'
    expected=$'0 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "single_node" {
    input=$'1 1'
    expected=$'1 1 1 1 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "two_nodes_right_child" {
    # Tree: 1 -> right 2
    input=$'2 1 2'
    expected=$'2 1 2 1 2 2 1 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "three_nodes_balanced" {
    # Tree:   2
    #        / \
    #       1   3
    input=$'3 2 1 3'
    expected=$'3 2 1 3 1 2 3 1 3 2 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "three_nodes_left_skewed" {
    # Tree: 3 -> left 2 -> left 1
    input=$'3 3 2 1'
    expected=$'3 3 2 1 1 2 3 1 2 3 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

@test "five_nodes_mixed" {
    # Tree with mixed inserts: 5 3 7 4 6
    input=$'5 5 3 7 4 6'
    expected=$'5 5 3 4 7 6 3 4 5 6 7 4 3 6 7 5 '
    result="$(echo "$input" | $RUN)"
    [ "$result" = "$expected" ]
}

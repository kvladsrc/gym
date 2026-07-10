#include "cpp/warmup/reorganize_string.hpp"

#include <utility>
#include <vector>

namespace warmup {

std::vector<int> reorganize_string_single_pass(std::vector<int> a) {
  std::vector<int> buf;
  std::vector<int> res;

  if (a.empty()) {
    return {};
  }

  res.push_back(a.back());
  a.pop_back();

  // Invariant: buf is empty or contains one repeated value. If its back and
  // a.back() differed, at most one could equal res.back(), so branch B or C
  // would run before branch D could put another value into buf.
  while (!a.empty() || !buf.empty()) {
    // Branch A: this is the only branch that may append an equal neighbor.
    if (a.empty()) {
      res.push_back(buf.back());
      buf.pop_back();
      continue;
    }

    // Branch B: buffered copies get first chance to use a separator.
    if (!buf.empty() && res.back() != buf.back()) {
      res.push_back(buf.back());
      buf.pop_back();
      continue;
    }

    // Branch C
    if (a.back() != res.back()) {
      res.push_back(a.back());
      a.pop_back();
      continue;
    }

    // Branch D: both available candidates equal res.back().
    buf.push_back(a.back());
    a.pop_back();
  }

  // Branches B and C never repeat res.back(), so the result is a
  // collision-free prefix followed by a block of one repeated value.
  return res;
}

std::vector<int> reorganize_string(std::vector<int> a) {
  // The second pass reads the repeated block first and buffers its extra
  // copies. Branch B then inserts one after every available separator from
  // the collision-free prefix. If copies remain, their value is necessarily
  // the most frequent one. With frequency M among n elements, the other
  // n - M elements create at most n - M + 1 slots, so every permutation has
  // at least max(0, 2*M - n - 1) repetitions. This pass leaves exactly that
  // many in its final block, attaining the lower bound.
  return reorganize_string_single_pass(
      reorganize_string_single_pass(std::move(a)));
}

}  // namespace warmup

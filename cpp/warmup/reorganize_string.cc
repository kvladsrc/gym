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

  while (!a.empty() || !buf.empty()) {
    // Branch A
    if (a.empty()) {
      res.push_back(buf.back());
      buf.pop_back();
      continue;
    }

    // Branch B
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

    // Branch D
    buf.push_back(a.back());
    a.pop_back();
  }

  return res;
}

std::vector<int> reorganize_string(std::vector<int> a) {
  // Two passes to optimize distribution
  return reorganize_string_single_pass(
      reorganize_string_single_pass(std::move(a)));
}

}  // namespace warmup

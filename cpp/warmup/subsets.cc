#include "cpp/warmup/subsets.hpp"

#include <cstdint>
#include <vector>

using std::vector;

namespace warmup {

vector<vector<int>> generate_subsets(const vector<int> &a) {
  auto power = a.size();
  vector<vector<int>> result;

  if (power > 63) {
    return result;
  }

  uint64_t powerset = 1ULL << power;

  for (uint64_t i = 0; i < powerset; ++i) {
    vector<int> subset;
    uint64_t ss = i;
    for (auto el : a) {
      if (ss % 2 != 0) {
        subset.push_back(el);
      }
      ss /= 2;
    }
    result.push_back(subset);
  }
  return result;
}

}  // namespace warmup

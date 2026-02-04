#include "cpp/warmup/kadanes.hpp"

#include <algorithm>
#include <cstdint>
#include <vector>

using std::max;
using std::vector;

namespace warmup {

/*
 * Kadane's Algorithm <3.
 */
int64_t kadanes(vector<int64_t> const& a) {
  int64_t global = 0;
  int64_t local = 0;
  for (auto i : a) {
    local = max(i, i + local);
    global = max(global, local);
  }
  return global;
}

}  // namespace warmup

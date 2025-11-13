#include <sys/types.h>

#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::endl;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int64_t l = 0;
  int64_t r = 0;
  std::cin >> l >> r;

  // Let represent l and r as a l_n...l_2l_1 and r_n...r_2r_1, where
  // l_k is 0 or 1. Next we need to find a maximal i, r_i !=
  // l_i. Because of constraints r_i = 1, l_i = 0 (or edge case r =
  // l). Then the answer is 1...1 (i times 1). We cannot change any
  // bit on j, j > i (it will be smaller than l or bigger than r). We
  // will have 1 on i position. Then for each 1 <= k < i we pick 1 for
  // a number (it still will be smaller than r and connot become
  // smaller than l) and 0 for b for the same reason.

  int64_t power = 1;
  while (power * 2 <= r) {
    power *= 2;
  }

  while (power > 0) {
    if ((r & power) != (l & power)) {
      break;
    }
    power /= 2;
  }

  int64_t res = 0;
  while (power > 0) {
    res += power;
    power /= 2;
  }

  std::cout << res << "\n";

  return 0;
}

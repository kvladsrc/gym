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
using std::gcd;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char* argv[]) {
  int n, k;
  std::cin >> n >> k;

  vector<int> theorems(n);
  for (auto& i : theorems) {
    std::cin >> i;
  }

  vector<int64_t> awake_sums(n + 1);
  vector<int64_t> sleep_sums(n + 1);
  awake_sums[0] = sleep_sums[0] = 0;
  for (int i = 1; i <= n; ++i) {
    int state;
    std::cin >> state;

    awake_sums[i] = awake_sums[i - 1];
    sleep_sums[i] = sleep_sums[i - 1];
    if (state) {
      awake_sums[i] += theorems[i - 1];
    } else {
      sleep_sums[i] += theorems[i - 1];
    }
  }

  int64_t best_use = 0;
  for (int i = 1; (i + k - 1) <= n; ++i) {
    auto from_i = sleep_sums[i + k - 1] - sleep_sums[i - 1];
    best_use = max(best_use, from_i);
  }

  std::cout << awake_sums.back() + best_use << "\n";

  return 0;
}

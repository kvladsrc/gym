#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
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

bool compare_pair(const pair<int, int> &a, const pair<int, int> &b) {
  if (a.second == b.second) {
    return a.first > b.first;
  }

  return a.second > b.second;
}

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;

  vector<int64_t> pre_sums(n + 1);
  vector<pair<int, int64_t>> negative;
  pre_sums[0] = 0;

  // We take all positive elements and zeroes.
  int res = 0;
  for (int i = 1; i <= n; ++i) {
    int64_t buf;
    std::cin >> buf;

    if (buf >= 0) {
      res++;
      pre_sums[i] = pre_sums[i - 1] + buf;
    } else {
      pre_sums[i] = pre_sums[i - 1];
      negative.push_back({i, buf});
    }
  }

  sort(negative.begin(), negative.end(), compare_pair);

  // Start by right smallest negative number. If we can not take it,
  // so we can not take same amout before it.
  for (auto p : negative) {
    bool can_take = true;
    for (int i = p.first; i <= n; ++i) {
      int64_t tmp = pre_sums[i] + p.second;
      if (tmp < 0) {
        can_take = false;
        break;
      }
    }

    if (can_take) {
      for (int i = p.first; i <= n; ++i) {
        pre_sums[i] += p.second;
      }
      res++;
    }
  }

  std::cout << res << "\n";

  return 0;
}

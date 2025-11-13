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

int main(int /*argc*/, char * /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;
  while ((t--) != 0) {
    size_t n = 0;
    std::cin >> n;
    vector<int64_t> a(n);
    int64_t abs_minimal = INT64_MAX;
    int neg = 0;
    int64_t abs_sum = 0;
    for (auto &i : a) {
      std::cin >> i;
      if (i < 0) {
        neg++;
      }
      abs_minimal = min(abs_minimal, abs(i));
      abs_sum += abs(i);
    }

    if (neg % 2 == 0) {
      std::cout << abs_sum << "\n";
    } else {
      std::cout << abs_sum - (abs_minimal * 2) << "\n";
    }
  }

  return 0;
}

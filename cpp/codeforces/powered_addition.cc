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

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    vector<int> a(n);
    for (auto& i : a) {
      std::cin >> i;
    }

    vector<int> optimal(n);
    optimal[0] = a[0];
    int max_delta = 0;
    for (int i = 1; i < n; ++i) {
      optimal[i] = max(optimal[i - 1], a[i]);
      max_delta = max(max_delta, optimal[i] - a[i]);
    }

    int res = 0;
    int64_t delta = 1;
    while ((delta - 1) < max_delta) {
      delta *= 2;
      res++;
    }

    std::cout << res << "\n";
  }

  return 0;
}

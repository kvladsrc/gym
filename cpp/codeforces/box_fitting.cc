#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <functional>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <system_error>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
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

int main(int argc, char* argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while (t--) {
    int n, w;
    std::cin >> n >> w;
    map<int, int, std::greater<int>> nums;
    for (int i = 0; i < n; ++i) {
      int buf;
      std::cin >> buf;
      nums[buf]++;
    }

    int res = 0;
    while (n) {
      res++;
      int avail = w;

      for (auto p : nums) {
        while (p.first <= avail && nums[p.first] > 0) {
          nums[p.first]--;
          n--;
          avail -= p.first;
        }
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}

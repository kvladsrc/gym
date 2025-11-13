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

int main(int argc, char *argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while (t--) {
    int n, k;
    std::cin >> n >> k;
    string s;
    std::cin >> s;
    map<char, int> m;
    for (auto c : s) {
      m[c]++;
    }

    int pairs = 0;
    int odd = 0;
    for (auto p : m) {
      pairs += p.second / 2;
      if (p.second % 2) {
        odd++;
      }
    }

    auto res = 0;
    res += (pairs / k) * 2;
    pairs %= k;
    if (pairs * 2 + odd >= k) {
      res++;
    }

    std::cout << res << "\n";
  }

  return 0;
}

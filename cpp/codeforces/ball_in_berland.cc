#include <algorithm>
#include <climits>
#include <cmath>
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
#include <utility>
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

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int a, b, k;
    std::cin >> a >> b >> k;
    vector<pair<int, int>> pairs(k);
    unordered_map<int, vector<int>> pairs_by_boys;
    unordered_map<int, vector<int>> pairs_by_girls;
    for (auto &p : pairs) {
      std::cin >> p.first;
    }
    for (auto &p : pairs) {
      std::cin >> p.second;
      pairs_by_boys[p.first].push_back(p.second);
      pairs_by_girls[p.second].push_back(p.first);
    }

    // Naive implementation k^2.

    int64_t res = 0;
    for (auto p : pairs) {
      res += k;
      res -= pairs_by_boys[p.first].size();
      res -= pairs_by_girls[p.second].size();
      // Substraction p itself twice.
      res++;
    }

    // p_1 and p_2 incompatible. We substracting p_1 while running on
    // p_1 and while running on p_2.
    res /= 2;

    std::cout << res << "\n";
  }

  return 0;
}

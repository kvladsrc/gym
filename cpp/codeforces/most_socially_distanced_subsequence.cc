#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
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

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    vector<int> p(n);
    for (auto &i : p) {
      std::cin >> i;
    }

    vector<int> res;
    res.push_back(p.front());
    for (int i = 1; i < (n - 1); ++i) {
      if (p[i - 1] < p[i] && p[i] < p[i + 1]) {
        continue;
      }
      if (p[i - 1] > p[i] && p[i] > p[i + 1]) {
        continue;
      }
      res.push_back(p[i]);
    }
    res.push_back(p.back());

    std::cout << res.size() << "\n";
    for (auto i : res) {
      std::cout << i << " ";
    }
    std::cout << "\n";
  }

  return 0;
}

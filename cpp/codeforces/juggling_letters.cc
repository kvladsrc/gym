#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
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

    vector<string> input(n);
    unordered_map<char, int> map;
    for (auto &s : input) {
      std::cin >> s;
      for (auto c : s) {
        map[c]++;
      }
    }

    bool res = std::none_of(map.begin(), map.end(),
                            [n](const auto &p) { return p.second % n; });

    std::cout << (res ? "YES" : "NO") << "\n";
  }

  return 0;
}

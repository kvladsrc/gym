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

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    vector<int> first;
    vector<int> second;
    vector<int> both;
    while (n--) {
      int m, s;
      std::cin >> m >> s;
      if (s == 1) {
        second.push_back(m);
      }
      if (s == 10) {
        first.push_back(m);
      }
      if (s == 11) {
        both.push_back(m);
      }
    }

    sort(first.begin(), first.end());
    sort(second.begin(), second.end());
    sort(both.begin(), both.end());

    int res = -1;
    if (first.empty()) {
      if (!both.empty()) {
        res = both.front();
      }
    } else if (second.empty()) {
      if (!both.empty()) {
        res = both.front();
      }
    } else {
      if (!both.empty()) {
        res = min(first.front() + second.front(), both.front());
      } else {
        res = first.front() + second.front();
      }
    }

    std::cout << res << "\n";
  }

  return 0;
}

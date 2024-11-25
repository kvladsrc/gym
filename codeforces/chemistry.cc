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
    int k;
    std::cin >> k;
    string s;
    std::cin >> s;

    unordered_map<char, int> cnt;
    for (auto c : s) {
      cnt[c]++;
    }

    int need_to_remove = 0;
    for (auto p : cnt) {
      need_to_remove += (p.second % 2);
    }
    if (need_to_remove) need_to_remove--;

    if (k >= need_to_remove && k < n) {
      std::cout << "YES"
                << "\n";
    } else {
      std::cout << "NO"
                << "\n";
    }
  }

  return 0;
}

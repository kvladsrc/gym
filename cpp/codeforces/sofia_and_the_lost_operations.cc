#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

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

int main(int argc, char* argv[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    vector<int> orig(n);
    for (auto& i : orig) {
      std::cin >> i;
    }

    map<int, int> need;
    // Checking if the last operation can be applyed.
    map<int, bool> used;

    for (int i = 0; i < n; ++i) {
      int buf;
      std::cin >> buf;
      used[buf] = true;

      if (orig[i] != buf) {
        need[buf]++;
      }
    }

    // Lets read d_m, last operation. element equal to d_m will be a
    // buffer for all useless operation, the value will be overrided
    // by last operation.
    map<int, int> ops;
    int m;
    std::cin >> m;
    for (int i = 0; i < (m - 1); ++i) {
      int buf;
      std::cin >> buf;
      ops[buf]++;
    }
    int last_operation;
    std::cin >> last_operation;
    ops[last_operation]++;

    // Last operation cannot be overrided.
    if (!used[last_operation]) {
      std::cout << "NO" << "\n";
      continue;
    }

    bool res = std::all_of(need.begin(), need.end(), [&](const auto& p) {
      return p.second <= ops[p.first];
    });

    std::cout << (res ? "YES" : "NO") << "\n";
  }

  return 0;
}

#include <algorithm>
#include <cassert>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
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
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int64_t cube(int64_t a) { return a * a * a; }

bool bin_search(int64_t a, vector<int64_t> &table) {
  int l = 0, r = table.size() - 1;
  int mid = (l + r) / 2;

  while (l < r) {
    if (table[mid] == a) return true;

    if (table[mid] < a) {
      l = mid + 1;
    } else {
      r = mid - 1;
    }

    mid = (l + r) / 2;
  }

  return table[l] == a;
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;

  vector<int64_t> table(10000);
  for (int i = 0; i < 10000; ++i) {
    table[i] = cube(i + 1);
  }

  while (t--) {
    int64_t x;
    std::cin >> x;

    int cur = 0;
    bool done = false;
    while (table[cur] <= (x + 1) / 2) {
      if (bin_search(x - table[cur++], table)) {
        done = true;
        break;
      }
    }

    if (done) {
      std::cout << "YES"
                << "\n";
    } else {
      std::cout << "NO"
                << "\n";
    }
  }

  return 0;
}

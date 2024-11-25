#include <algorithm>
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

int binary_search(vector<int64_t> &table, int64_t val) {
  if (table[0] > val) return -1;
  if (table[table.size() - 1] < val) return -1;

  int l = 0, r = table.size() - 1;
  int mid = (l + r) / 2;

  while (l < r) {
    if (table[mid] == val) {
      return mid;
    }

    if (table[mid] > val) {
      r = mid - 1;
    } else {
      l = mid + 1;
    }

    mid = (r + l) / 2;
  }

  return (table[l] == val ? l : -1);
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;

  while (t--) {
    int n;
    std::cin >> n;

    uint64_t total = 0;
    while (n--) {
      int64_t buf;
      std::cin >> buf;
      total += buf;
    }

    int64_t r = 1;
    while (r * r < total) {
      r *= 2;
    }

    int64_t l = r / 2;
    int64_t mid = (l + r) / 2;
    bool res = false;
    while (r - l > 1) {
      if (mid * mid < total) {
        l = mid;
      } else {
        if (mid * mid == total) {
          res = true;
          break;
        }

        r = mid;
      }

      mid = (l + r) / 2;
    }

    if (res || l * l == total || r * r == total) {
      std::cout << "YES"
                << "\n";
    } else {
      std::cout << "NO"
                << "\n";
    }
  }

  return 0;
}

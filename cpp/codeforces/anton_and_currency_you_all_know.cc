#include <algorithm>
#include <climits>
#include <cmath>
#include <cstddef>
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

int ctoi(char c) { return c - '0'; }

void swap(char &a, char &b) {
  char buf = a;
  a = b;
  b = buf;
}

int main(int argc, char *argv[]) {
  string cur;
  std::cin >> cur;

  // Need to swap last digin to get even number.
  if (ctoi(cur.back() % 2 == 1)) {
    // Trying to find leftmost even digit less than last.
    int best = -1;
    bool swapped = false;
    for (size_t i = 0; i < cur.size() - 1; ++i) {
      if (ctoi(cur[i]) % 2 == 0) {
        best = i;
        if (cur[i] < cur.back()) {
          swap(cur[i], cur.back());
          swapped = true;
          break;
        }
      }
    }

    // No even digits or all greater than last.
    if (!swapped) {
      if (best != -1) {
        swap(cur.back(), cur[best]);
      } else {
        // All digits are odd. Cannot solve.
        std::cout << -1 << "\n";
        return 0;
      }
    }
  } else {
    for (size_t i = 0; i < cur.size(); ++i) {
      size_t best = i;
      for (size_t j = i + 1; j < cur.size(); ++j) {
        if (cur[best] < cur[j]) {
          best = j;
        }
      }

      if (cur[i] < cur[best]) {
        if (best != cur.size() - 1) {
          swap(cur[i], cur[best]);
          break;
        } else if (ctoi(cur[i]) % 2 == 0) {
          swap(cur[i], cur[best]);
          break;
        }
      } else {
        if (ctoi(cur[cur.size() - 2]) % 2 == 0) {
          swap(cur.back(), cur[cur.size() - 2]);
        } else {
          if (cur.size() < 3) {
            std::cout << -1 << "\n";
            return 0;
          } else {
            swap(cur[cur.size() - 2], cur[cur.size() - 3]);
          }
        }
      }
    }
  }

  std::cout << cur << "\n";
}

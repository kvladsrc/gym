#include "cpp/warmup/dutch_flag.hpp"

#include <cstddef>
#include <iostream>
#include <string>
#include <utility>
#include <vector>

using std::string;
using std::vector;

namespace warmup {

/*
  Sort an array of elements 1, 2 or 3.
*/
void dutch_flag(vector<int> &a) {
  // EDGE_CASE: Empty or one element.
  if (a.size() <= 1) {
    return;
  }

  std::size_t l = 0;
  std::size_t m = 0;
  std::size_t r = a.size() - 1;

  while (m <= r) {
    switch (a[m]) {
      case 1:
        std::swap(a[m], a[l]);
        m++;
        l++;
        break;
      case 2:
        m++;
        break;
      default:
        std::swap(a[m], a[r]);
        if (r == 0) {
          return;
        }
        r--;
    }
  }
}

}  // namespace warmup

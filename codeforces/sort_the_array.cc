#include <algorithm>
#include <cstddef>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

void print_prop(vector<int> &orig) {
  int first, last;
  int switched = 0;

  for (size_t i = 1; i < orig.size(); ++i) {
    if (switched == 0) {
      if (orig[i] < orig[i - 1]) {
        first = i - 1;
        switched = 1;
      }

      continue;
    }

    if (switched == 1) {
      if (orig[i] > orig[i - 1]) {
        last = i - 1;
        switched = 2;
      }

      continue;
    }

    if (switched == 2) {
      if (orig[i] < orig[i - 1]) {
        std::cout << "no"
                  << "\n";
        return;
      }
    }
  }

  if (switched == 0) {
    std::cout << "yes\n1 1\n";
    return;
  }

  if (switched == 1) {
    last = orig.size() - 1;
  }

  if (first > 0 && orig[first - 1] > orig[last]) {
    std::cout << "no"
              << "\n";
    return;
  }

  if (last < static_cast<int>(orig.size()) - 1 &&
      orig[first] > orig[last + 1]) {
    std::cout << "no"
              << "\n";
    return;
  }

  std::cout << "yes"
            << "\n";
  std::cout << first + 1 << " ";
  std::cout << last + 1 << "\n";
}

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  vector<int> orig(n);
  for (int i = 0; i < n; ++i) {
    std::cin >> orig[i];
  }
  print_prop(orig);

  return 0;
}

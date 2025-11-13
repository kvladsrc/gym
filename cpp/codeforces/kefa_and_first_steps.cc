#include <algorithm>
#include <iostream>
#include <string>
#include <unordered_map>
#include <vector>

using std::max;
using std::min;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;

  int res = 0, prev_sub = 1, prev_elem;
  std::cin >> prev_elem;

  for (int i = 1; i < n; ++i) {
    int buf;
    std::cin >> buf;
    if (buf >= prev_elem) {
      prev_sub++;
    } else {
      if (res < prev_sub) res = prev_sub;
      prev_sub = 1;
    }
    prev_elem = buf;
  }
  std::cout << (res > prev_sub ? res : prev_sub) << "\n";

  return 0;
}

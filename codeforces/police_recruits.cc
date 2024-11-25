#include <algorithm>
#include <iostream>
#include <string>
#include <unordered_map>
#include <vector>

using std::max;
using std::min;
using std::pair;
using std::sort;
using std::string;
using std::unordered_map;
using std::vector;

int main(int argc, char *argv[]) {
  int n, available = 0, crimes = 0;
  std::cin >> n;
  while (n) {
    int buf;
    std::cin >> buf;
    if (buf == -1) {
      if (available) {
        available--;
      } else {
        crimes++;
      }
    } else {
      available += buf;
    }
    n--;
  }
  std::cout << crimes << "\n";

  return 0;
}

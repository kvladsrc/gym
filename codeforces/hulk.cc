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
  bool hate = true;
  while (n) {
    if (hate) {
      std::cout << "I hate";
    } else {
      std::cout << "I love";
    }
    hate = !hate;
    n--;
    if (n) std::cout << " that ";
  }
  std::cout << " it"
            << "\n";

  return 0;
}

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
  unordered_map<int, bool> m;
  int missing = 0;
  for (int i = 0; i < 4; ++i) {
    int key;
    std::cin >> key;
    if (m[key]) {
      missing++;
      continue;
    }
    m[key] = true;
  }
  std::cout << missing << "\n";

  return 0;
}
